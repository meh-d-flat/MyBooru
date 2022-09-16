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

        public async Task<Media> DownloadAsync(string id)
        {
            Media file = null;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getFileQuery = "SELECT * FROM Medias WHERE Hash = @a";

            using (SQLiteCommand getFile = new SQLiteCommand(getFileQuery, connection))
            {
                getFile.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                var result = await getFile.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));
                }
                await result.DisposeAsync();
            }

            string getTagsQuery =
                @"SELECT Tags.ID, Tags.Name FROM Medias 
                JOIN MediasTags ON Medias.id = MediasTags.MediaID
                JOIN Tags ON Tags.ID = MediasTags.TagID
                Where Hash = @a;";
            using (SQLiteCommand getTags = new SQLiteCommand(getTagsQuery, connection))
            {
                getTags.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                var result = await getTags.ExecuteReaderAsync();

                if (result.HasRows)
                    file.Tags = TableCell.MakeEntities<Tag>(TableCell.GetRows(result));

                await result.DisposeAsync();
            }
            await connection.CloseAsync();
            return file;
        }

        public async Task<List<Media>> DownloadAsync(int page, int reverse)
        {
            List<Media> files = new List<Media>();
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getFilesQuery = "SELECT * FROM Medias" + (reverse == 1 ? " ORDER BY Id DESC " : " ") +  $"LIMIT 20 OFFSET { 20 * (page - 1) }";

            using (SQLiteCommand getFiles = new SQLiteCommand(getFilesQuery, connection))
            {
                getFiles.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = page, DbType = System.Data.DbType.Int32 });
                var result = await getFiles.ExecuteReaderAsync();

                if (result.HasRows)
                    files = TableCell.MakeEntities<Media>(TableCell.GetRows(result));

                await result.DisposeAsync();
            }

            await connection.CloseAsync();
            return files;
        }
    }
}
