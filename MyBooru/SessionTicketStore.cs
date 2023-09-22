using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru
{
    public class SessionTicketStore : ITicketStore
    {
        private IConfiguration _config;
        private readonly IHttpContextAccessor _contextAccessor;
        private IQueryService _queryService;

        public SessionTicketStore(IConfiguration config, IHttpContextAccessor contextAccessor)
        {
            _config = config;
            _contextAccessor = contextAccessor;
            _queryService = contextAccessor.HttpContext.RequestServices.GetService<IQueryService>();
        }

        public async Task RemoveAsync(string key)
        {
            await _queryService.QueryTheDb<Task>(async x => 
            {
                x.Parameters.AddNew("@a", key, System.Data.DbType.String);
                await x.ExecuteNonQueryAsync();
                return Task.CompletedTask;
            }, "DELETE FROM Tickets WHERE ID = @a;");
        }

        public async Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            string removeTicketQuery = "UPADTE Tickets SET Value = @a WHERE ID = @b; UPDATE Tickets SET LastActivity = @c WHERE ID = @b;";
            await _queryService.QueryTheDb<Task>(async x =>
            {
                x.Parameters.AddNew("@a", Serialize(ticket), System.Data.DbType.Binary);
                x.Parameters.AddNew("@b", key, System.Data.DbType.String);
                x.Parameters.AddNew("@c", (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds, System.Data.DbType.Int32);
                await x.ExecuteNonQueryAsync();
                return Task.CompletedTask;
            }, removeTicketQuery);
        }

        public async Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            var ticket = new Ticket();
            await _queryService.QueryTheDb<Ticket>(async x =>
            {
                x.Parameters.AddNew("@a", key, System.Data.DbType.String);
                var result = await x.ExecuteReaderAsync();

                if (result.HasRows)
                {
                    while (await result.ReadAsync())
                        ticket = TableCell.MakeEntity<Ticket>(TableCell.GetRow(result));
                    return ticket;
                }
                else 
                    return ticket;
            }, "SELECT * FROM Tickets WHERE ID = @a");

            return Deserialize(ticket.Value);
        }

        public async Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            string id = ticket.Principal.FindFirstValue("uniqueId");
            string addTicketQuery = "INSERT INTO Tickets ('ID', 'Username', 'Value', 'LastActivity', 'UserAgent', 'IP') VALUES (@a, @b, @c, @d, @e, @f)";

            await _queryService.QueryTheDb<Task>(async x =>
            {
                x.Parameters.AddNew("@a", id, System.Data.DbType.String);
                x.Parameters.AddNew("@b", ticket.Principal.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name).Value, System.Data.DbType.String);
                x.Parameters.AddNew("@c", Serialize(ticket), System.Data.DbType.Binary);
                x.Parameters.AddNew("@d", (int)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds, System.Data.DbType.Int32);
                x.Parameters.AddNew("@e", _contextAccessor.HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "User-Agent").Value, System.Data.DbType.String);
                x.Parameters.AddNew("@f", _contextAccessor.HttpContext.Connection.RemoteIpAddress, System.Data.DbType.String);
                await x.ExecuteNonQueryAsync();
                return Task.CompletedTask;
            }, addTicketQuery);

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
