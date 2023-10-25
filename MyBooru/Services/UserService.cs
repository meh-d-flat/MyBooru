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

        public async Task<bool> CheckPasswordNewAsync(string email, string password, CancellationToken ct)
        {
            bool passwordChecksOut = false;
            bool saltChecksOut = false;

            User user = await queryService.QueryTheDbAsync<User>(async x =>
            {
                x.Parameters.AddNew("@a", email, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntity<User>(await TableCell.GetRowAsync(result));
            }, "SELECT * FROM Users WHERE Email = @a");

            using (var hmac = new HMACSHA512(user.PasswordSalt))
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var computedHash = await hmac.ComputeHashAsync(new MemoryStream(passwordBytes));
                saltChecksOut = hmac.Key.SequenceEqual(user.PasswordSalt);
                passwordChecksOut = computedHash.SequenceEqual(user.PasswordHash);
            }
            return passwordChecksOut & saltChecksOut;
        }

        public async Task<User> GetUserAsync(string username, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<User>(async x =>
            {
                x.Parameters.AddNew("@a", username, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntity<User>(await TableCell.GetRowAsync(result));
            }, "SELECT Username, Role, RegisterDateTime FROM Users WHERE Username = @a");
        }

        public async Task<User> GetUserNewAsync(string email, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<User>(async x =>
            {
                x.Parameters.AddNew("@a", email, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntity<User>(await TableCell.GetRowAsync(result));
            }, "SELECT * FROM Users WHERE Email = @a");
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
            await connection.OpenAsync(ct);
            var userCommand = TableCell.MakeAddCommand<User>(new User()
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                Role = "User",
                RegisterDateTime = DateTime.UtcNow.GetUnixTime()
            }, connection);
            await userCommand.ExecuteNonQueryAsync(ct);
            await userCommand.DisposeAsync();
            await connection.CloseAsync();
            return 1;
        }

        public async Task<List<Ticket>> GetUserSessionsAsync(string sessionId, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<List<Ticket>>(async x =>
            {
                x.Parameters.AddNew("@a", sessionId, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntities<Ticket>(await TableCell.GetRowsAsync(result));
            }, "SELECT * FROM Tickets WHERE Username = (SELECT Username FROM Tickets WHERE ID = @a)");
        }

        public async Task<bool> CloseUserSessionAsync(string sessionId, string email)
        {
            return await queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@b", email, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync());
            }, "DELETE FROM Tickets WHERE ID = @a AND Username = (SELECT Username From Users WHERE Email = @b)");
        }

        public async Task<bool> ChangePasswordAsync(string email, string oldPass, string newPass, string sessionId, CancellationToken ct)
        {
            var passChecked = await CheckPasswordNewAsync(email, oldPass, ct);
            if (!passChecked)
                return passChecked;

            byte[] passwordHash = new byte[2];
            byte[] passwordSalt = new byte[2];

            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = await hmac.ComputeHashAsync(new MemoryStream(Encoding.UTF8.GetBytes(newPass)));
            }

            var newPassChecked = await queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", email, System.Data.DbType.String);
                x.Parameters.AddNew("@b", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@c", passwordSalt, System.Data.DbType.Binary);
                x.Parameters.AddNew("@d", passwordHash, System.Data.DbType.Binary);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync(ct));
            }, "UPDATE Users SET PasswordSalt = @c, PasswordHash = @d WHERE Username = (SELECT Username From Users WHERE Email = @a) AND Username = (SELECT Username FROM Tickets WHERE ID = @b)");

            return passChecked & newPassChecked;
        }

        public async Task<bool> ChangeEmailAsync(string oldMail, string newMail, string sessionId, CancellationToken ct)
        {
            return await queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", oldMail, System.Data.DbType.String);
                x.Parameters.AddNew("@b", sessionId, System.Data.DbType.String);
                x.Parameters.AddNew("@c", newMail, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync(ct));
            }, "UPDATE Users SET Email = @c WHERE Username = (SELECT Username From Users WHERE Email = @a) AND Username = (SELECT Username FROM Tickets WHERE ID = @b)");
        }
    }
}