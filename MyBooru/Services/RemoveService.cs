using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class RemoveService : Contracts.IRemoveService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;

        public RemoveService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
        }

        public async Task<string> RemoveAsync(string id)
        {
            string removed = "deleted";
            Media file = null;

            await queryService.QueryTheDbAsync<Media>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        file = TableCell.MakeEntity<Media>(TableCell.GetRow(result));
                }

                return null;
            }, "SELECT * FROM Medias WHERE Hash = @a");

            try
            {
                await Task.Run(() => Directory.Delete(Path.GetFullPath(file.Path).Replace(Path.GetFileName(file.Path), ""), true));
            }
            catch (Exception ex)
            {
                removed = $"error: {ex.GetType()} {ex.Message}";
            }
            //removed = await queryService.QueryTheDbAsync<string>(async x => 
            //{
            //    x.Parameters.AddNew("@a", id, System.Data.DbType.String);
            //    await x.ExecuteNonQueryAsync();
            //    return removed;
            //}, "DELETE FROM MediasTags WHERE MediaID = (SELECT ID FROM Medias WHERE Hash = @a);DELETE FROM Medias WHERE Hash = @a;");
            return removed;
        }
    }
}
