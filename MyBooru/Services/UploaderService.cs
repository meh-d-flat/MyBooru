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
using MyBooru.Models;
using static System.Net.Mime.MediaTypeNames;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace MyBooru.Services
{
    public class UploadService : Contracts.IUploadService
    {
        readonly IConfiguration config;

        public UploadService(IConfiguration configuration)
        {
            config = configuration;
        }

        public async Task<string> UploadOneAsync(IFormFile file, string username)
        {
            string fileHash = "empty";
            var up = new Media();
            var ffmpegPaths = config.GetSection("FFMpegExecPath").Get<List<string>>();
            var ffmpegPath = Ext.IsWindows() ? ffmpegPaths[0] : ffmpegPaths[1];
            var ffprobePaths = config.GetSection("FFProbeExecPath").Get<List<string>>();
            var ffprobePath = Ext.IsWindows() ? ffprobePaths[0] : ffprobePaths[1];
            var dimensionsParsed = false;
            var dimensions = new int[] { -1, -1 };
            var ffOutput = "";
            var scale = "scale=300:-1";

            if (file == null)
                return fileHash;

            if (!(file.ContentType.Contains("image") | file.ContentType.Contains("video")))
                return fileHash;

            if (!File.Exists(ffmpegPath))
                return "error: ffmpeg not found";

            if (!File.Exists(ffprobePath))
                return "error: ffprobe not found";

            bool illegalChar = file.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 | file.FileName.Contains("%");
                up.Name = illegalChar ? new Random().Next().ToString() + Path.GetExtension(file.FileName) : file.FileName;

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            up.Type = file.ContentType;
            up.Uploader = username;
            var guid = Guid.NewGuid().ToString();
            var directoryPath = Path.Combine(config.GetValue<string>("FilePath"), guid);
            var path = Path.Combine(directoryPath, up.Name);

            using (var stream = file.OpenReadStream())
            {
                using (SHA256 SHA256 = SHA256Managed.Create())
                {
                    string hash = BitConverter.ToString(await SHA256.ComputeHashAsync(stream));
                    hash = hash.Replace("-", "");
                    up.Hash = hash;
                    fileHash = hash;
                }

                await Task.Run(() => Directory.CreateDirectory(directoryPath));
                up.Path = path;

                using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(fileStream);
                }

                up.Timestamp = DateTime.UtcNow.GetUnixTime();

                var thumbName = $"{new Random().Next()}_thumbnail.jpeg";
                var fullPath = Path.GetFullPath(path);
                var thumbPath = Path.GetFullPath(path).Replace(Path.GetFileName(path), thumbName);
                var webThumbPath = path.Replace(Path.GetFileName(path), thumbName);

                var ffprobe = new System.Diagnostics.Process();
                ffprobe.StartInfo.FileName = ffprobePath;
                ffprobe.StartInfo.RedirectStandardOutput = true;
                ffprobe.EnableRaisingEvents = true;
                ffprobe.StartInfo.UseShellExecute = false;
                ffprobe.StartInfo.Arguments = 
                    $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=,:p=0 \"{fullPath}\"";
                ffprobe.OutputDataReceived += (s, e) => ffOutput += e.Data;

                try
                {
                    ffprobe.Start();
                    ffprobe.BeginOutputReadLine();
                    ffprobe.WaitForExit();
                    ffprobe.CancelOutputRead();
                    ffprobe.Close();
                    ffprobe.Dispose();
                    dimensionsParsed = 
                        (int.TryParse(ffOutput.Split(",")[0], out var width) & width > 0) 
                        & (int.TryParse(ffOutput.Split(",")[1], out var height) & height > 0);
                    dimensions = dimensionsParsed ? new int[] { width, height} : dimensions;
                    Debug.WriteLine($"dimension parse fail {fullPath}");
                }
                catch (Exception ex)
                {
                    ffprobe.Close();
                    ffprobe.Dispose();
                    Debug.WriteLine($"dimension parse exception {fullPath}\n{ex}");
                }

                scale = !dimensionsParsed ? scale : dimensions[0] > dimensions[1] ? scale : "scale=-1:300";

                var ffmpeg = new System.Diagnostics.Process();
                ffmpeg.StartInfo.FileName = ffmpegPath;
                ffmpeg.StartInfo.Arguments = file.ContentType.Contains("video") 
                    ? $"-i \"{fullPath}\" -ss 00:00:00.000 -vframes 1 -vf {scale} \"{thumbPath}\"" 
                    : $"-i \"{fullPath}\" -vf {scale} \"{thumbPath}\"";

                try
                {
                    ffmpeg.Start();
                    if (!ffmpeg.WaitForExit(1000))
                        await Task.Run(() => Directory.Delete(Path.GetDirectoryName(path), true));

                    ffmpeg.WaitForExit();
                    ffmpeg.Close();
                    ffmpeg.Dispose();
                }
                catch
                {
                    fileHash = "error: failed to create thumbnail";
                    ffmpeg.Close();
                    ffmpeg.Dispose();
                }

                up.Thumb = webThumbPath;
            }
            var addFile = TableCell.MakeAddCommand<Media>(up,connection);

            try
            {
                await addFile.ExecuteNonQueryAsync();
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine(ex.Message);
                await Task.Run(() => Directory.Delete(Path.GetDirectoryName(path), true));
                fileHash = ex.Message.Contains("UNIQUE") ? "error: such file already exists" : "error: failed to upload";
            }
            finally
            {
                await addFile.DisposeAsync();
            }
            await connection.CloseAsync();

            return fileHash;
        }

        public async Task<List<string>> UploadManyAsync(List<IFormFile> files, string username)
        {
            var hashes = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                var hash = await UploadOneAsync(files[i], username);
                hashes.Add(hash);
            }
            return hashes;
        }
    }
}
