using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MyBooru
{
    public class SessionTicketStore : ITicketStore
    {
        private IConfiguration _config;
        private readonly IHttpContextAccessor _contextAccessor;

        public SessionTicketStore(IConfiguration config, IHttpContextAccessor contextAccessor)
        {
            _config = config;
            _contextAccessor = contextAccessor;
        }

        public async Task RemoveAsync(string key)
        {
            using var connection = new SQLiteConnection(_config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string removeTicketQuery = "DELETE FROM Tickets WHERE ID = @a;";
            using (SQLiteCommand removeTicket = new SQLiteCommand(removeTicketQuery, connection))
            {
                removeTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = key, DbType = System.Data.DbType.String });
                await removeTicket.ExecuteNonQueryAsync();
                await removeTicket.DisposeAsync();
                await connection.CloseAsync();
            }
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            using var connection = new SQLiteConnection(_config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string removeTicketQuery = "UPADTE Tickets SET Value = @a WHERE ID = @b; UPDATE Tickets SET LastActivity = @c WHERE ID = @b;";
            using (SQLiteCommand removeTicket = new SQLiteCommand(removeTicketQuery, connection))
            {
                removeTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = Serialize(ticket), DbType = System.Data.DbType.Binary });
                removeTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@b", Value = key, DbType = System.Data.DbType.String });
                removeTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@c", Value = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds, DbType = System.Data.DbType.Int32 });
                await removeTicket.ExecuteNonQueryAsync();
                await removeTicket.DisposeAsync();
                await connection.CloseAsync();
            }
        }

        public async Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            var ticket = new Ticket();
            using var connection = new SQLiteConnection(_config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string getTicketQuery = "SELECT * FROM Tickets WHERE ID = @a";

            using (SQLiteCommand getTicket = new SQLiteCommand(getTicketQuery, connection))
            {
                getTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = key, DbType = System.Data.DbType.String });
                var result = await getTicket.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        ticket = TableCell.MakeEntity<Ticket>(TableCell.GetRow(result));
                }
                await result.DisposeAsync();
            }

            await connection.CloseAsync();

            return Deserialize(ticket.Value);
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            string id = ticket.Principal.FindFirstValue("uniqueId");
            using var connection = new SQLiteConnection(_config.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();
            string addTicketQuery = "INSERT INTO Tickets ('ID', 'Username', 'Value', 'LastActivity', 'UserAgent', 'IP') VALUES (@a, @b, @c, @d, @e, @f)";

            using (SQLiteCommand addTicket = new SQLiteCommand(addTicketQuery, connection))
            {
                addTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@a", Value = id, DbType = System.Data.DbType.String });
                addTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@b", Value = ticket.Principal.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name).Value, DbType = System.Data.DbType.String });
                addTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@c", Value = Serialize(ticket), DbType = System.Data.DbType.Binary });
                addTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@d", Value = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds, DbType = System.Data.DbType.Int32 });
                addTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@e", Value = _contextAccessor.HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "User-Agent").Value, DbType = System.Data.DbType.String });
                addTicket.Parameters.Add(new SQLiteParameter() { ParameterName = "@f", Value = _contextAccessor.HttpContext.Connection.RemoteIpAddress, DbType = System.Data.DbType.String });

                try
                {
                    await addTicket.ExecuteNonQueryAsync();
                }
                catch (SQLiteException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ticket creation error: {ex}");
                }
                finally
                {
                    await addTicket.DisposeAsync();
                }
            }

            await connection.CloseAsync();
            return id;
        }

        private byte[] Serialize(AuthenticationTicket source)
        {
            return TicketSerializer.Default.Serialize(source);
        }

        private AuthenticationTicket Deserialize(byte[] source)
        {
            return source == null ? null : TicketSerializer.Default.Deserialize(source);
        }
    }
}
