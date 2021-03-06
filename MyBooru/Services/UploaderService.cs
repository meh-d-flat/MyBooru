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

        public string UploadOne(IFormFile file)
        {
            string fileHash = "empty";

            if (!(file.ContentType.Contains("image") | file.ContentType.Contains("video")))
                return fileHash;

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string addFileQuery = "INSERT INTO Medias ('Name', 'Hash', 'Type', 'Path', 'Thumb') VALUES (@a, @b, @c, @d, @e)";//('Name', 'Hash', 'Size', 'Type', 'Binary', 'Path') VALUES (@a, @b, @c, @d, @e, @f)

            SQLiteCommand addFile = new SQLiteCommand(addFileQuery, connection);
            addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = file.FileName, DbType = System.Data.DbType.String });
            addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@c", Value = file.ContentType, DbType = System.Data.DbType.String });

            using (var stream = file.OpenReadStream())
            {
                int size = (int)file.Length;
                byte[] bytes = new byte[size];
                stream.Read(bytes, 0, (int)file.Length);

                using (SHA256 SHA256 = SHA256Managed.Create())
                {
                    string hash = BitConverter.ToString(SHA256.ComputeHash(bytes));
                    hash = hash.Replace("-", "");
                    addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@b", Value = hash, DbType = System.Data.DbType.String });
                    fileHash = hash;
                }

                var guid = Guid.NewGuid().ToString();
                var directoryPath = Path.Combine(config.GetValue<string>("FilePath"), guid);
                Directory.CreateDirectory(directoryPath);
                var path = Path.Combine(directoryPath, file.FileName);
                var webPath = path.Replace(@"\", "/");
                addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@d", Value = webPath, DbType = System.Data.DbType.String });

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    file.CopyTo(fileStream);
                }

                var fullPath = Path.GetFullPath(path);
                var thumbPath = Path.GetFullPath(path).Replace(Path.GetFileName(path), "thumbnail.jpeg");
                var webThumbPath = path.Replace(Path.GetFileName(path), "thumbnail.jpeg").Replace(@"\", "/");
                var ffmpeg = new System.Diagnostics.Process();
                //ffmpeg.StartInfo.FileName = @"ffmpeg\bin\ffmpeg.exe";
                ffmpeg.StartInfo.FileName = config["FFMpegExecPath"];
                ffmpeg.StartInfo.Arguments = file.ContentType.Contains("video") ? $"-i {fullPath} -ss 00:00:00.001 -vframes 1 -vf scale=300:-1 {thumbPath}" : $"-i {fullPath} -vf scale=300:-1 {thumbPath}";
                ffmpeg.Start();
                ffmpeg.WaitForExit();

                addFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@e", Value = webThumbPath, DbType = System.Data.DbType.String });
            }

            try
            {
                addFile.ExecuteNonQuery();
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine(ex);
                fileHash = $"error: {ex.GetType()} {ex.Message}";
            }
            finally
            {
                addFile.Dispose();
            }
            connection.Close();

            return fileHash;
        }

        public List<string> UploadMany(ICollection<IFormFile> files)
        {
            var hashes = new List<string>();
            foreach (var media in files)
            {
                var hash = UploadOne(media);
                hashes.Add(hash);
            }
            return hashes;
        }
    }
}
