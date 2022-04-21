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
            string getFileQuery = "SELECT * FROM Medias WHERE Hash = @a";

            using (SQLiteCommand getFile = new SQLiteCommand(getFileQuery, connection))
            {
                getFile.Parameters.AddWithValue("@a", id);
                var result = getFile.ExecuteReader();

                if (result.HasRows)
                {
                    while (result.Read())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));
                }
                result.Dispose();
            }

            string getTagsQuery =
                @"SELECT Tags.ID, Tags.Name FROM Medias 
                JOIN MediasTags ON Medias.id = MediasTags.MediaID
                JOIN Tags ON Tags.ID = MediasTags.TagID
                Where Hash = @a;";
            using (SQLiteCommand getTags = new SQLiteCommand(getTagsQuery, connection))
            {
                getTags.Parameters.AddWithValue("@a", id);
                var result = getTags.ExecuteReader();
                if (result.HasRows)
                {
                    file.Tags = TableCell.MakeEntities<Tag>(TableCell.GetRows(result));
                }
            }
            connection.Close();
            return file;
        }

        public List<Media> Download(int page)
        {
            List<Media> files = new List<Media>();
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            connection.Open();
            string getFilesQuery = $"SELECT * FROM Medias LIMIT 20 OFFSET { 20 * page }";

            using (SQLiteCommand getFiles = new SQLiteCommand(getFilesQuery, connection))
            {
                getFiles.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = page, DbType = System.Data.DbType.Int32 });
                var result = getFiles.ExecuteReader();

                if (result.HasRows)
                    files = TableCell.MakeEntities<Media>(TableCell.GetRows(result));

                result.Dispose();
            }

            connection.Close();
            return files;
        }
    }
}
