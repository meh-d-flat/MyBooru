using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

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
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string addFileQuery = "INSERT INTO Medias ('Name', 'Hash', 'Size', 'Type', 'Binary') VALUES (@a, @b, @c, @d, @e)";

            SQLiteCommand addFile = new SQLiteCommand(addFileQuery, connection);
            addFile.Parameters.AddWithValue("@a", file.FileName);
            addFile.Parameters.AddWithValue("@d", file.ContentType);

            using (var stream = file.OpenReadStream())
            {
                int size = (int)file.Length;
                addFile.Parameters.AddWithValue("@c", size);
                byte[] bytes = new byte[size];
                stream.Read(bytes, 0, (int)file.Length);
                addFile.Parameters.AddWithValue("@e", bytes);

                using (SHA256 SHA256 = SHA256Managed.Create())
                {
                    string hash = BitConverter.ToString(SHA256.ComputeHash(bytes));
                    hash = hash.Replace("-", "");
                    addFile.Parameters.AddWithValue("@b", hash);
                    fileHash = hash;
                }
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
