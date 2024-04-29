using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyBooru.Models;
using MyBooru.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly Contracts.IUserService _userService;

        public UserController(Contracts.IUserService userService)
        {
            _userService = userService;
        }

        [Authorize(Policy = "IsLogged"), Route("getInfo")]
        public async Task<IActionResult> GetInfo(CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(HttpContext.User.Identity.Name, ct);
            return new JsonResult(new {
                username = user.Username,
                dateRegistered = user.RegisterDateTime,
                role = user.Role,
            });
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Get()
        {
            //HttpContext.Request.Cookies.ToList().ForEach(x => Debug.WriteLine($"Cookie - {x.Key} : {x.Value}"));
            //Debug.WriteLine($"Auth type {HttpContext.User.Identity.AuthenticationType}");
            //Debug.WriteLine($"{HttpContext.User.Identity.IsAuthenticated}");
            return Ok("Login successful");
        }

        [HttpGet, Route("checklogin")]
        public IActionResult Check()
        {
            if (HttpContext.User.Identity.IsAuthenticated)
                return Ok();

            return Unauthorized();
        }

        [HttpGet, Route("details"), Authorize]
        public async Task<IActionResult> Details(string username, CancellationToken ct)
        {
            var user = await _userService.GetUserAsync(username, ct);
            return user == null 
                ? NotFound()
                : Ok(new JsonResult(new 
                {
                    username = user.Username,
                    dateRegistered = user.RegisterDateTime,
                    role = user.Role,
                }));
        }

        [HttpPost, Route("signup")]
        async public Task<IActionResult> SignUp([FromForm] string username, [FromForm] string email, [FromForm] string password, [FromForm] string passwordRepeat, CancellationToken ct)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
                return RedirectToAction("Details");

            if (password != passwordRepeat)
                return BadRequest("Password mismatch!");

            if (await _userService.CheckUsernameAsync(username))
                return BadRequest("Username/Email already registered!");

            if (await _userService.CheckEmailAsync(email))
                return BadRequest("Username/Email already registered!");

            await _userService.PersistUserAsync(username, password, email, ct);

            return await this.SignIn(email, password, ct);
        }

        [HttpPost, Route("signin")]
        async public Task<IActionResult> SignIn([FromForm] string email, [FromForm] string password, CancellationToken ct)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
                return RedirectToAction("Details");

            if (!await ShuffledCredCheck(_userService, email, password, ct))
                return BadRequest("Wrong Username/Password combination");

            var user = await _userService.GetUserNewAsync(email, ct);

            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("uniqueId", Guid.NewGuid().ToString())
                };

            var claimsIdentity = new ClaimsIdentity(
                claims, "bla.bla");

            var authProperties = new AuthenticationProperties();

            await HttpContext.SignInAsync(
                    "bla.bla",
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

            //return RedirectToAction("Details");
            return Ok();
        }

        [Route("signoff")]
        public async Task<IActionResult> SignOff(bool fromAJAX)
        {
            await HttpContext.SignOutAsync("bla.bla");
            HttpContext.Response.Cookies.Delete("SESSION");

            return fromAJAX ? new EmptyResult() : RedirectToAction("SignIn");
        }

        [Authorize(Policy = "IsLogged"), Route("getSessions")]
        public async Task<IActionResult> GetSessions(CancellationToken ct)
        {
            var all = await _userService.GetUserSessionsAsync(HttpContext.User.FindFirstValue("uniqueId"), ct);

            var formatted = all.Select(x => new
            {
                IsActiveSession = TicketSerializer.Default.Deserialize(x.Value).Principal.Claims.FirstOrDefault(x => x.Type == "uniqueId").Value == HttpContext.User.Claims.FirstOrDefault(x => x.Type == "uniqueId").Value,
                UserAgent = x.UserAgent,
                LastActivity = x.LastActivity,
                IP = x.IP,
                id = x.ID
            });

            return new JsonResult(new { allSessions = formatted });
        }

        [Authorize(Policy = "IsLogged"), Route("closeSession")]
        public async Task<IActionResult> CloseSession(string sessionId)
        {
            if (sessionId == HttpContext.User.FindFirstValue("uniqueId"))
                return BadRequest();

            var closed = await _userService.CloseUserSessionAsync(sessionId, HttpContext.User.FindFirstValue(ClaimTypes.Email));
            return closed ? Ok() : BadRequest();
        }

        [HttpPost, Route("changePass"), Authorize(Policy = "IsLogged")]
        public async Task<IActionResult> ChangePassword([FromForm] string newPass, [FromForm] string oldPass, [FromForm] string newPassrepeat, CancellationToken ct)
        {
            if (newPass != newPassrepeat)
                return BadRequest();

            var sessionId = HttpContext.User.FindFirstValue("uniqueId");
            var email = HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email).Value;
            var checks = await _userService.ChangePasswordAsync(email, oldPass, newPass, sessionId, ct);
            return checks ? (IActionResult)Ok() : StatusCode(500);
        }

        [HttpPost, Route("changeMail"), Authorize(Policy = "IsLogged")]
        public async Task<IActionResult> ChangeEmail([FromForm]string newMail, CancellationToken ct)
        {
            var oldMail = HttpContext.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email).Value;
            var sessionId = HttpContext.User.FindFirstValue("uniqueId");
            var changed = await _userService.ChangeEmailAsync(oldMail, newMail, sessionId, ct);
            if (!changed)
                return StatusCode(500);
            else
            {
                var id = HttpContext.User.Identity as ClaimsIdentity;
                id.RemoveClaim(id.FindFirst(x => x.Type == ClaimTypes.Email));
                id.AddClaim(new Claim(ClaimTypes.Email, newMail));
                return Ok();
            }
        }

        public static async Task<bool> ShuffledCredCheck(Contracts.IUserService us, string email, string password, CancellationToken ct)
        {
            Func<Task<bool>> un = async () => await us.CheckEmailAsync(email);
            Func<Task<bool>> pw = async () => await us.CheckPasswordNewAsync(email, password, ct);
            Func<Task<bool>> rw = async () => { await Task.Delay(new Random().Next(5, 80), ct); return true; };
            var credCheckers = new[]{ un, pw, rw }.OrderBy(x => new Random().Next()).ToArray();
            bool checkedOut = true;
            for (int i = 0; i < 2; i++)
            {
                checkedOut = checkedOut & await credCheckers[i].Invoke();
            }
            return checkedOut;
        }
    }
}