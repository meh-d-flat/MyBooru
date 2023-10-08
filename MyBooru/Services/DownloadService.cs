using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading;
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

        public async Task<Media> DownloadAsync(string id, CancellationToken ct)
        {
            Media file = null;
            file = await queryService.QueryTheDbAsync<Media>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntity<Media>(await TableCell.GetRowAsync(result));
            }, "SELECT * FROM Medias WHERE Hash = @a");

            file.Tags = await queryService.QueryTheDbAsync<List<Tag>>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntities<Tag>(await TableCell.GetRowsAsync(result));
            }, @"SELECT Tags.Name FROM Medias 
                JOIN MediasTags ON Medias.id = MediasTags.MediaID
                JOIN Tags ON Tags.ID = MediasTags.TagID
                Where Hash = @a;");

            file.Comments = await queryService.QueryTheDbAsync<List<Comment>>(async x =>
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntities<Comment>(await TableCell.GetRowsAsync(result));
            },"SELECT Id, Text, User, Timestamp FROM Comments WHERE MediaID = @a");

            return file;
        }

        public async Task<List<Media>> DownloadAsync(int page, int reverse, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<List<Media>>(async x => 
            {
                x.Parameters.AddNew("@a", page, System.Data.DbType.Int32);
                var result = await x.ExecuteReaderAsync(ct);
                return result.HasRows ? TableCell.MakeEntities<Media>(await TableCell.GetRowsAsync(result)) : new List<Media>();
            }, "SELECT Thumb, Hash FROM Medias" + (reverse == 1 ? " ORDER BY Id DESC " : " ") + $"LIMIT 20 OFFSET {20 * (page - 1)}");
        }
    }
}
