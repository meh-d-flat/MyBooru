using Microsoft.Extensions.Configuration;
using System;
using System.Data.SQLite;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class QueryService : IQueryService
    {
        private readonly IConfiguration config;

        public QueryService(IConfiguration config)
        {
            this.config = config;
        }

        public async Task<T> QueryTheDbAsync<T>(Func<SQLiteCommand, Task<T>> f, string query)
        {
            T output = default(T);
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            using var command = new SQLiteCommand(query, connection);
            output = await f(command);
            await connection.CloseAsync();
            return output;
        }
    }
}