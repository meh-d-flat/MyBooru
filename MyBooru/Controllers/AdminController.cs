using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace MyBooru.Controllers
{
    public class AdminController : Controller
    {
        [HttpGet]
        public IActionResult Get()
        {
            if (!HttpContext.User.Identity.IsAuthenticated)
                return StatusCode(400);//NotFound();

            if (HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Role).Value != "Admin")
                return StatusCode(500);//NotFound();

            return Ok("Hello admin");
        }

        [HttpGet]
        public IActionResult Stuff() => Ok("it works!");

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
