using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Services
{
    public class DownloadService : Contracts.IDownloadService
    {
        readonly IConfiguration config;

        public DownloadService(IConfiguration configuration)
        {
            config = configuration;
        }

        public Media Download(string id)
        {
            Media file = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string getFileQuery = String.Format("SELECT * FROM Medias WHERE Hash = '{0}'", id);

            using (SQLiteCommand getFile = new SQLiteCommand(getFileQuery, connection))
            {
                var result = getFile.ExecuteReader();

                if (result.HasRows)
                {
                    while (result.Read())
                    {
                        int size = result.GetInt32(3);
                        byte[] bytes = new byte[size];
                        //returns number of bytes in blob
                        result.GetBytes(5, 0, bytes, 0, size);

                        file = new Media
                        {
                            Id = result.GetInt32(0),
                            Name = result.GetString(1),
                            Hash = result.GetString(2),
                            Size = size,
                            Type = result.GetString(4),
                            Binary = bytes
                        };
                    }
                }
                result.Dispose();
            }
            connection.Close();
            return file;
        }
    }
}
