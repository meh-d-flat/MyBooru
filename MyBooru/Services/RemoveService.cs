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

        public async Task<string> RemoveAsync(string id, string sessionId, string email)
        {
            string removed = "deleted";
            string path = "none";

            path = await queryService.QueryTheDbAsync<string>(async x => 
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                x.Parameters.AddNew("@b", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@c", email, System.Data.DbType.String);
                return Convert.ToString(await x.ExecuteScalarAsync());
            }, @"SELECT Path FROM Medias WHERE Hash = @a
                AND Medias.Uploader = (SELECT Username FROM Tickets WHERE ID = @b AND Username = (SELECT Username From Users WHERE Email = @c))");

            if (path == string.Empty | path == "none")
                return "error: unauthorized to delete";

            try
            {
                await Task.Run(() => Directory.Delete(Path.GetFullPath(path).Replace(Path.GetFileName(path), string.Empty), true));
            }
            catch (Exception ex)
            {
                removed = $"error: {ex.GetType()} {ex.Message}";
            }
            return await queryService.QueryTheDbAsync<string>(async x =>
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                await x.ExecuteNonQueryAsync();
                return removed;
            }, "DELETE FROM Medias WHERE Hash = @a;");
        }
    }
}
