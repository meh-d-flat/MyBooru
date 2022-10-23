﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{
    public class UserService : IUserService
    {
        readonly IConfiguration config;

        public UserService(IConfiguration configuration)
        {
            config = configuration;
        }

        public async Task<bool> CheckEmailAsync(string email)
        {
            bool exists = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string checkEmailExistsQuery = "SELECT COUNT(*) FROM Users WHERE Email = @p";

            using (SQLiteCommand checEmailkExists = new SQLiteCommand(checkEmailExistsQuery, connection))
            {
                checEmailkExists.Parameters.Add(new SQLiteParameter() { ParameterName = "@p", Value = email, DbType = System.Data.DbType.String });
                exists = Convert.ToBoolean(await checEmailkExists.ExecuteScalarAsync());
            }

            await connection.CloseAsync();
            return exists;
        }

        public async Task<bool> CheckUsernameAsync(string username)
        {
            bool exists = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string checkUsernameExistsQuery = "SELECT COUNT(*) FROM Users WHERE Username = @p";

            using (SQLiteCommand checkUsernameExists = new SQLiteCommand(checkUsernameExistsQuery, connection))
            {
                checkUsernameExists.Parameters.Add(new SQLiteParameter() { ParameterName = "@p", Value = username, DbType = System.Data.DbType.String });
                exists = Convert.ToBoolean(await checkUsernameExists.ExecuteScalarAsync());
            }

            await connection.CloseAsync();
            return exists;
        }

        public async Task<bool> CheckPasswordAsync(string username, string password)
        {
            var user = await GetUserAsync(username);
            bool passwordChecksOut = false;

            using (var hmac = new HMACSHA512(user.PasswordSalt))
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var computedHash = await hmac.ComputeHashAsync(new MemoryStream(passwordBytes));
                passwordChecksOut = computedHash.SequenceEqual(user.PasswordHash);
            }
            return passwordChecksOut;
        }

        public async Task<User> GetUserAsync(string username)
        {
            var user = new User();
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getUserQuery = "SELECT * FROM Users WHERE Username = @a";

            using (SQLiteCommand getUser = new SQLiteCommand(getUserQuery, connection))
            {
                getUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = username, DbType = System.Data.DbType.String });
                var result = await getUser.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        user = TableCell.MakeEntity<User>(TableCell.GetRow(result));
                }
                await result.DisposeAsync();
            }

            await connection.CloseAsync();

            return user;
        }

        public async Task<User> PersistUserAsync(string username, string password, string email)
        {
            var user = new User();
            byte[] passwordHash = new byte[2];
            byte[] passwordSalt = new byte[2];

            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = await hmac.ComputeHashAsync(new MemoryStream(Encoding.UTF8.GetBytes(password)));
            }

            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string addUserQuery = "INSERT INTO Users ('Username', 'Email', 'PasswordHash', 'PasswordSalt', 'Role', 'RegisterDateTime') VALUES (@a, @b, @c, @d, @e, @f)";

            using (SQLiteCommand addUser = new SQLiteCommand(addUserQuery, connection))
            {
                addUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = username, DbType = System.Data.DbType.String });
                addUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@b", Value = email, DbType = System.Data.DbType.String });
                addUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@c", Value = passwordHash, DbType = System.Data.DbType.Binary });
                addUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@d", Value = passwordSalt, DbType = System.Data.DbType.Binary });
                addUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@e", Value = "User", DbType = System.Data.DbType.String });
                addUser.Parameters.Add(new SQLiteParameter() { ParameterName = "@f", Value = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds, DbType = System.Data.DbType.Int32 });

                try
                {
                    await addUser.ExecuteNonQueryAsync();
                }
                catch (SQLiteException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"user creation error: {ex}");
                }
                finally
                {
                    await addUser.DisposeAsync();
                }
            }

            await connection.CloseAsync();

            return await GetUserAsync(username);
        }

        public async Task<List<Ticket>> GetUserSessionsAsync(string username)
        {
            var tickets = new List<Ticket>();
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getTicketQuery = "SELECT * FROM Tickets WHERE Username = @a";

            using (SQLiteCommand getTicket = new SQLiteCommand(getTicketQuery, connection))
            {
                getTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = username, DbType = System.Data.DbType.String });
                var result = await getTicket.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        tickets = TableCell.MakeEntities<Ticket>(TableCell.GetRows(result));
                }
                await result.DisposeAsync();
            }

            await connection.CloseAsync();

            return tickets;
        }

        public async Task<bool> CloseUserSessionAsync(string sessionId)
        {
            bool closed = false;
            using var connection = new SQLiteConnection(config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getTicketQuery = "DELETE FROM Tickets WHERE ID = @a";

            using (SQLiteCommand getTicket = new SQLiteCommand(getTicketQuery, connection))
            {
                getTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = sessionId, DbType = System.Data.DbType.String });
                var result = await getTicket.ExecuteNonQueryAsync();
                closed = Convert.ToBoolean(result);
            }

            await connection.CloseAsync();

            return closed;
        }
    }
}
