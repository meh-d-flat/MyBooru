using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class UserService : IUserService
    {
        readonly IConfiguration config;
        private readonly IQueryService queryService;

        public UserService(IConfiguration configuration, IQueryService queryService)
        {
            config = configuration;
            this.queryService = queryService;
        }

        public async Task<bool> CheckEmailAsync(string email)
        {
            return await queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@p", email, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteScalarAsync());
            }, "SELECT COUNT(*) FROM Users WHERE Email = @p");
        }

        public async Task<bool> CheckUsernameAsync(string username)
        {
            return await queryService.QueryTheDbAsync<bool>(async x => 
            {
                x.Parameters.AddNew("@p", username, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteScalarAsync());
            }, "SELECT COUNT(*) FROM Users WHERE Username = @p");
        }

        public async Task<bool> CheckPasswordAsync(string username, string password, CancellationToken ct)
        {
            var user = await GetUserAsync(username, ct);
            bool passwordChecksOut = false;

            using (var hmac = new HMACSHA512(user.PasswordSalt))
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var computedHash = await hmac.ComputeHashAsync(new MemoryStream(passwordBytes));
                passwordChecksOut = computedHash.SequenceEqual(user.PasswordHash);
            }
            return passwordChecksOut;
        }

        public async Task<User> GetUserAsync(string username, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<User>(async x =>
            {
                var user = new User();
                x.Parameters.AddNew("@a", username, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                if (result.HasRows)
                {
                    while (await result.ReadAsync(ct))
                        user = TableCell.MakeEntity<User>(TableCell.GetRow(result));
                    return user;
                }
                else
                    return null;
            }, "SELECT * FROM Users WHERE Username = @a");
        }

        public async Task<int> PersistUserAsync(string username, string password, string email, CancellationToken ct)
        {
            int result = -1;
            byte[] passwordHash = new byte[2];
            byte[] passwordSalt = new byte[2];

            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = await hmac.ComputeHashAsync(new MemoryStream(Encoding.UTF8.GetBytes(password)));
            }
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await queryService.QueryTheDbAsync<Task>(async x => 
            {
                x = TableCell.MakeAddCommand<User>(new User()
                {
                    Username = username,
                    Email = email,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    Role = "User",
                    RegisterDateTime = DateTime.UtcNow.GetUnixTime()
                }, connection);

                return Task.CompletedTask;
            }, "");
            return 1;
        }

        public async Task<List<Ticket>> GetUserSessionsAsync(string sessionId, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<List<Ticket>>(async x =>
            {
                x.Parameters.AddNew("@a", sessionId, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                return result.HasRows ? TableCell.MakeEntities<Ticket>(await TableCell.GetRowsAsync(result)) : null;
            }, @"SELECT * FROM Tickets WHERE Username = (SELECT Username FROM Tickets WHERE ID = @a)");
        }

        public async Task<bool> CloseUserSessionAsync(string sessionId, string email)
        {
            return await queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@b", email, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync());
            }, @"DELETE FROM Tickets WHERE ID = @a AND Username = (SELECT Username From Users WHERE Email = @b)");
        }
    }
}
