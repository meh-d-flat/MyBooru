﻿using Microsoft.AspNetCore.Authentication;
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
using System.Threading.Tasks;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [Authorize(Roles = "User"), Route("getInfo")]
        public async Task<IActionResult> GetInfo([FromServices] UserService userService)
        {
            var user = await userService.GetUserAsync(HttpContext.User.Identity.Name);
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
        public IActionResult Details()
        {
            return Ok($"Signed in as: {HttpContext.User.Identity.Name}");
        }

        [HttpPost, Route("signup")]
        async public Task<IActionResult> SignUp([FromServices] UserService userService, [FromForm] string username, [FromForm] string email, [FromForm] string password, [FromForm] string passwordRepeat)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
                return RedirectToAction("Details");

            if (password != passwordRepeat)
                return BadRequest("Password mismatch!");

            if (await userService.CheckUsernameAsync(username))
                return BadRequest("Username/Email already registered!");

            if (await userService.CheckEmailAsync(email))
                return BadRequest("Username/Email already registered!");

            var user = await userService.PersistUserAsync(username, password, email);

            return await this.SignIn(userService, username, password);
        }

        [HttpPost, Route("signin")]
        async public Task<IActionResult> SignIn([FromServices] UserService userService, [FromForm] string username, [FromForm] string password)
        {
            if (HttpContext.User.Identity.IsAuthenticated)
                return RedirectToAction("Details");

            if (!await userService.CheckUsernameAsync(username))
                return BadRequest("Wrong Username/Password combination");

            if (!await userService.CheckPasswordAsync(username, password))
                return BadRequest("Wrong Username/Password combination");

            var user = await userService.GetUserAsync(username);

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

        [Authorize(Roles = "User"), Route("getSessions")]
        public async Task<IActionResult> GetSessions([FromServices] UserService userService)
        {
            var all = await userService.GetUserSessionsAsync(HttpContext.User.Identity.Name);

            var formatted = all.Select(x => new
            {
                IsActiveSession = TicketSerializer.Default.Deserialize(x.Value).Principal.Claims.FirstOrDefault(x => x.Type == "uniqueId").Value == HttpContext.User.Claims.FirstOrDefault(x => x.Type == "uniqueId").Value,
                UserAgent = x.UserAgent,
                LastActivity = x.LastActivity,
                IP = x.IP,
                id = x.ID//,
                //UniqueId = TicketSerializer.Default.Deserialize(x.Value).Principal.Claims.FirstOrDefault(x => x.Type == "uniqueId").Value
            });

            return new JsonResult(new { allSessions = formatted });
        }

        [Authorize(Roles = "User"), Route("closeSession")]
        public async Task<IActionResult> CloseSession([FromServices] UserService userService, string sessionId)
        {
            if (sessionId == HttpContext.User.Claims.FirstOrDefault(x => x.Type == "uniqueId").Value)
                return BadRequest();

            var closed = await userService.CloseUserSessionAsync(sessionId);
            return closed ? Ok() : BadRequest();
        }
    }
}
