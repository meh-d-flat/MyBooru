using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Drawing;
using System.Net.Mime;

namespace MyBooru.Services
{
    public class UploadService : Contracts.IUploadService
    {
        readonly IConfiguration config;

        public UploadService(IConfiguration configuration)
        {
            config = configuration;
        }

        public async Task<string> UploadOneAsync(IFormFile file)
        {
            string fileHash = "empty";
            string webPath, webThumbPath;

            if (file == null)
                return fileHash;

            if (!(file.ContentType.Contains("image") | file.ContentType.Contains("video")))
                return fileHash;

            if (!File.Exists(config["FFMpegExecPath"]))
                return "error: ffmpeg not found";

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string addFileQuery = "INSERT INTO Medias ('Name', 'Hash', 'Type', 'Path', 'Thumb') VALUES (@a, @b, @c, @d, @e)";//('Name', 'Hash', 'Size', 'Type', 'Binary', 'Path') VALUES (@a, @b, @c, @d, @e, @f)

            SQLiteCommand addFile = new SQLiteCommand(addFileQuery, connection);
            addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = file.FileName, DbType = System.Data.DbType.String });
            addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@c", Value = file.ContentType, DbType = System.Data.DbType.String });

            using (var stream = file.OpenReadStream())
            {
                //int size = (int)file.Length;
                //byte[] bytes = new byte[size];
                //await stream.ReadAsync(bytes, 0, (int)file.Length);

                using (SHA256 SHA256 = SHA256Managed.Create())
                {
                    string hash = BitConverter.ToString(await SHA256.ComputeHashAsync(stream));
                    hash = hash.Replace("-", "");
                    addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@b", Value = hash, DbType = System.Data.DbType.String });
                    fileHash = hash;
                }

                var guid = Guid.NewGuid().ToString();
                var directoryPath = Path.Combine(config.GetValue<string>("FilePath"), guid);
                await Task.Run(() => Directory.CreateDirectory(directoryPath));
                var path = Path.Combine(directoryPath, file.FileName);
                webPath = path.Replace(@"\", "/");
                addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@d", Value = webPath, DbType = System.Data.DbType.String });

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(fileStream);
                }

                var fullPath = Path.GetFullPath(path);
                var thumbPath = Path.GetFullPath(path).Replace(Path.GetFileName(path), "thumbnail.jpeg");
                webThumbPath = path.Replace(Path.GetFileName(path), "thumbnail.jpeg").Replace(@"\", "/");
                var ffmpeg = new System.Diagnostics.Process();
                ffmpeg.StartInfo.FileName = config["FFMpegExecPath"];
                ffmpeg.StartInfo.Arguments = file.ContentType.Contains("video") ? $"-i \"{fullPath}\" -ss 00:00:00.001 -vframes 1 -vf scale=300:-1 \"{thumbPath}\"" : $"-i \"{fullPath}\" -vf scale=300:-1 \"{thumbPath}\"";

                try
                {
                    ffmpeg.Start();
                    if (!ffmpeg.WaitForExit(2000))
                    {
                        ffmpeg.Close();
                        await Task.Run(() => Directory.Delete(Path.GetDirectoryName(webPath), true));
                    }
                }
                catch
                {
                    fileHash = "thumb creation error";
                }

                addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@e", Value = webThumbPath, DbType = System.Data.DbType.String });
            }

            try
            {
                await addFile.ExecuteNonQueryAsync();
            }
            catch (SQLiteException ex)
            {
                await Task.Run(() => Directory.Delete(Path.GetDirectoryName(webPath), true));
                fileHash = $"error: {ex.GetType()} {ex.Message}";
            }
            finally
            {
                await addFile.DisposeAsync();
            }
            await connection.CloseAsync();

            return fileHash;
        }

        public async Task<List<string>> UploadManyAsync(ICollection<IFormFile> files)
        {
            var hashes = new List<string>();
            foreach (var media in files)
            {
                var hash = await UploadOneAsync(media);
                hashes.Add(hash);
            }
            return hashes;
        }
    }
}
