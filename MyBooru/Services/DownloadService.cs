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
            string getFileQuery = $"SELECT * FROM Medias WHERE Hash = '{id}'";

            using (SQLiteCommand getFile = new SQLiteCommand(getFileQuery, connection))
            {
                var result = getFile.ExecuteReader();

                if (result.HasRows)
                {
                    while (result.Read())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));

                }
                result.Dispose();
            }
            connection.Close();
            return file;
        }
    }
}
