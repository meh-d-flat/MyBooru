﻿using Microsoft.AspNetCore.Http;
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

            if (file == null)
                return fileHash;

            if (!(file.ContentType.Contains("image") | file.ContentType.Contains("video")))
                return fileHash;

            if (!File.Exists(ffmpegPath))
                return "error: ffmpeg not found";

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
                var ffmpeg = new System.Diagnostics.Process();
                ffmpeg.StartInfo.FileName = ffmpegPath;
                ffmpeg.StartInfo.Arguments = file.ContentType.Contains("video") ? $"-i \"{fullPath}\" -ss 00:00:00.000 -vframes 1 -vf scale=300:-1 \"{thumbPath}\"" : $"-i \"{fullPath}\" -vf scale=300:-1 \"{thumbPath}\"";

                try
                {
                    ffmpeg.Start();
                    if (!ffmpeg.WaitForExit(2000))
                    {
                        ffmpeg.Close();
                        await Task.Run(() => Directory.Delete(Path.GetDirectoryName(path), true));
                    }
                    ffmpeg.WaitForExit();
                }
                catch
                {
                    fileHash = "error: failed to create thumbnail";
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
