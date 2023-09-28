using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class DownloadService : Contracts.IDownloadService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;

        public DownloadService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
        }

        public async Task<Media> DownloadAsync(string id)
        {
            Media file = null;
            file = await queryService.QueryTheDb<Media>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();
                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));

                    return file;
                }
                else
                    return null;
            }, "SELECT * FROM Medias WHERE Hash = @a");

            var _ = await queryService.QueryTheDb<List<Tag>>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();

                if (result.HasRows)
                    file.Tags = TableCell.MakeEntities<Tag>(await TableCell.GetRowsAsync(result));

                return null;
            }, @"SELECT Tags.ID, Tags.Name FROM Medias 
                JOIN MediasTags ON Medias.id = MediasTags.MediaID
                JOIN Tags ON Tags.ID = MediasTags.TagID
                Where Hash = @a;");

            return file;
        }

        public async Task<List<Media>> DownloadAsync(int page, int reverse)
        {
            return await queryService.QueryTheDb<List<Media>>(async x => 
            {
                x.Parameters.AddNew("@a", page, System.Data.DbType.Int32);
                var result = await x.ExecuteReaderAsync();
                return result.HasRows ? TableCell.MakeEntities<Media>(await TableCell.GetRowsAsync(result)) : new List<Media>();
            }, "SELECT * FROM Medias" + (reverse == 1 ? " ORDER BY Id DESC " : " ") + $"LIMIT 20 OFFSET {20 * (page - 1)}");
        }
    }
}
