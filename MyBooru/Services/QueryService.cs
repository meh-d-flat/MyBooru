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
            query = $"PRAGMA foreign_keys=on;{query}";//yeah...
            using var command = new SQLiteCommand(query, connection);
            try
            {
                output = await f(command);
            }
            catch (SQLiteException ex)
            {
                Console.WriteLine(ex.Message);
                await connection.CloseAsync();
                await command.DisposeAsync();
            }
            await connection.CloseAsync();
            return output;
        }
    }
}