using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Data.SQLite;
using static MyBooru.Services.Contracts;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MyBooru.Models;
using MyBooru.Services;
using System.IO;
using Microsoft.AspNetCore.Authentication;
using System;

//first demote the mods and then do whatever i guess?
namespace MyBooru.Controllers
{
    /// <summary>
    /// I am aware of auth middleware, its handlers and reqs.
    /// But this one's is security-through-obscurity hidden route solely.
    /// </summary>
    public class AdminController : Controller
    {
        private readonly IConfiguration _conf;
        private readonly IQueryService _queryService;
        public AdminController(IConfiguration configuration, IQueryService queryService)
        {
            _conf = configuration;
            _queryService = queryService;
        }

        bool CheckAdmin(HttpContext ctx)
        {
            if (!ctx.User.Identity.IsAuthenticated)
                return false;

            if (ctx.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role).Value != "Admin")
                return false;

            return true;
        }

        bool CheckCreds(HttpContext ctx)
        {
            if (!ctx.User.Identity.IsAuthenticated)
                return false;

            var role = ctx.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role).Value;
            if (role != "Admin" && role != "Moder")
                return false;

            return true;
        }

        async Task<IEnumerable<string>> Orphans(CancellationToken ct)
        {
            var smth = await _queryService.QueryTheDbAsync<List<Media>>(async x =>
            {
                var result = await x.ExecuteReaderAsync(ct);
                return result.HasRows ? TableCell.MakeEntities<Media>(await TableCell.GetRowsAsync(result)) : new List<Media>();
            }, "SELECT Path FROM Medias");

            var dbFiles = smth.Select(x => Path.GetDirectoryName(x.Path));
            var fsFiles = Directory.GetDirectories(_conf.GetValue<string>("FilePath"));
            return fsFiles.Except(dbFiles);
        }

        /// <summary>
        /// Gets files on fs and not on db
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> OrphansList(CancellationToken ct)
        {
            if (!CheckCreds(HttpContext))
                return NotFound();

            var result = await Orphans(ct);
            var orphanFiles = result.Select(x =>
                new
                {
                    folder = x,
                    file = new DirectoryInfo(x).EnumerateFiles().Aggregate((a, b) => b.Length > a.Length ? b : a).Name//not Max or OrderByDesc
                }
            );

            return new JsonResult(orphanFiles);
        }

        public async Task<IActionResult> GetUserSesh(string username, CancellationToken ct)
        {
            if (!CheckCreds(HttpContext))
                return NotFound();

            if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
                return BadRequest("username was empty!");

            var all = await _queryService.QueryTheDbAsync<List<Ticket>>(async x =>
            {
                x.Parameters.AddNew("@a", username, System.Data.DbType.String);
                using var result = await x.ExecuteReaderAsync(ct);
                return TableCell.MakeEntities<Ticket>(await TableCell.GetRowsAsync(result));
            }, "SELECT * FROM Tickets WHERE Username = @a AND Tickets.Username != (SELECT Users.Username FROM Users WHERE Users.Role = 'Admin')");

            all = all.OrderByDescending(x => x.LastActivity).ToList();

            var formatted = all.Select(x => new
            {
                x.ID,
                x.UserAgent,
                x.IP,
                x.LastActivity
            });

            return new JsonResult(new { allSessions = formatted });
        }

        public async Task<IActionResult> CloseUserSesh(string sessionId, CancellationToken ct)
        {
            if (!CheckCreds(HttpContext))
                return NotFound();

            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrWhiteSpace(sessionId))
                return BadRequest("sessionID was empty!");

            var seshId = HttpContext.User.Claims.FirstOrDefault(x => x.Type == "unique-id").Value;

            var done = await _queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", sessionId, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync(ct));
            }, "DELETE FROM Tickets WHERE ID = @a AND Tickets.Username NOT IN (SELECT Users.Username FROM Users WHERE Users.Role = 'Admin' OR Users.Role = 'Moder')");

            return done ? Ok() : StatusCode(500);
        }

        public async Task<IActionResult> BanStatusChange(string username, bool? intentIsToBan, CancellationToken ct)
        {
            if (!CheckCreds(HttpContext))
                return NotFound();

            if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
                return BadRequest("username was empty!");

            if (!intentIsToBan.HasValue)
                return BadRequest("intent was empty");

            var seshId = HttpContext.User.Claims.FirstOrDefault(x => x.Type == "unique-id").Value;

            var done = await _queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", intentIsToBan.Value ? "Ban" : "User", System.Data.DbType.String);
                x.Parameters.AddNew("@b", username, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync(ct));
            }, @"UPDATE Users Set Role = @a WHERE Users.Username = @b AND Users.Role != 'Moder' AND Users.Role != 'Admin';
                DELETE FROM Tickets WHERE Tickets.Username = @b AND Tickets.Username NOT IN (SELECT Users.Username FROM Users WHERE Users.Role = 'Admin' OR Users.Role = 'Moder');");

            return done ? Ok($"{(intentIsToBan.Value ? "banned" : "revoked")} moder status for user {username}") : StatusCode(500);
        }

        public async Task<IActionResult> ModerStatusChange(string username, bool? intentIsToGrant, CancellationToken ct)
        {
            if (!CheckAdmin(HttpContext))
                return NotFound();

            if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(username))
                return BadRequest("username was empty!");

            if (!intentIsToGrant.HasValue)
                return BadRequest("intent was empty");

            var newRole = intentIsToGrant.Value ? "Moder" : "User";

            var done = await _queryService.QueryTheDbAsync<bool>(async x =>
            {
                x.Parameters.AddNew("@a", newRole, System.Data.DbType.String);
                x.Parameters.AddNew("@b", username, System.Data.DbType.String);
                return Convert.ToBoolean(await x.ExecuteNonQueryAsync(ct));
            }, @"UPDATE Users Set Role = @a WHERE Users.Username = @b AND Users.Role != 'Admin';
                DELETE FROM Tickets WHERE Tickets.Username = @b AND Tickets.Username != (SELECT Users.Username FROM Users WHERE Users.Role = 'Admin');");

            return done ? Ok($"{(intentIsToGrant.Value ? "granted" : "revoked")} moder status for user {username}") : StatusCode(500);
        }
    }
}
