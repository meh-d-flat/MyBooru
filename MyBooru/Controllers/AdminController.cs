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

namespace MyBooru.Controllers
{
    public class AdminController : Controller
    {
        private readonly IConfiguration _conf;
        private readonly IQueryService _queryService;
        public AdminController(IConfiguration configuration, IQueryService queryService)
        {
            _conf = configuration;
            _queryService = queryService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            if (CheckCreds(HttpContext))
                return NotFound();

            return Ok("Hello admin");
        }

        /// <summary>
        /// Gets files on fs and not on db
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Orphans(CancellationToken ct)
        {
            if (!CheckCreds(HttpContext))
                return NotFound();

            using var connection = new SQLiteConnection(_conf.GetSection("Store:ConnectionString").Value);
            await connection.OpenAsync();

            var smth = await _queryService.QueryTheDbAsync<List<Media>>(async x =>
            {
                var result = await x.ExecuteReaderAsync(ct);
                return result.HasRows ? TableCell.MakeEntities<Media>(await TableCell.GetRowsAsync(result)) : new List<Media>();
            }, "SELECT Path FROM Medias");

            var dbFiles = smth.Select(x => Path.GetDirectoryName(x.Path)).ToArray();
            var fsFiles = Directory.GetDirectories(_conf.GetValue<string>("FilePath"));
            //var orphanFiles = fsFiles.Except(dbFiles).Select(x => x = $"{x}\\{new DirectoryInfo(x).GetFiles().OrderByDescending(y => y.Length).FirstOrDefault().Name}").ToArray();
            var orphanFiles = fsFiles.Except(dbFiles)
                .Select(x => 
                new 
                {
                    folder = x,
                    file = new DirectoryInfo(x).EnumerateFiles().Aggregate((a,b) => b.Length > a.Length ? b : a).Name//not Max or OrderByDesc
                });

            return new JsonResult(orphanFiles);
        }

        bool CheckCreds(HttpContext ctx)
        {
            if (!ctx.User.Identity.IsAuthenticated)
                return false;

            if (ctx.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role).Value != "Admin")
                return false;

            return true;
        }
    }
}
