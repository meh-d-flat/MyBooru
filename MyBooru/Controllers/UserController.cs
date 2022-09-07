using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyBooru.Models;
using System;
using System.Collections.Generic;
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
        static User user = new User();

        [Authorize]
        public IActionResult Get()
        {
            return Ok("Login successful");
        }

        [HttpPost, Route("signup")]
        async public Task<IActionResult> SignUp([FromForm] string username, [FromForm] string email, [FromForm] string password, [FromForm] string passwordRepeat)
        {
            if (password != passwordRepeat)//check whether username already exists
                return BadRequest("Password mismatch!");

            using (var hmac = new HMACSHA512())
            {
                user.PasswordSalt = hmac.Key;
                user.PasswordHash = await hmac.ComputeHashAsync(new MemoryStream(Encoding.UTF8.GetBytes(password)));
            }

            user.Username = username;
            user.RegisterDateTime = DateTime.Now;
            user.Email = email;//validate this

            return Ok(user);//go into sign in
        }

        [HttpPost, Route("signin")]
        async public Task<IActionResult> SignIn([FromForm] string username, [FromForm] string password)
        {
            bool passwordChecksOut = false;
            if (user.Username != username)
                return BadRequest("User not found");

            using (var hmac = new HMACSHA512(user.PasswordSalt))
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var computedHash = await hmac.ComputeHashAsync(new MemoryStream(passwordBytes));
                passwordChecksOut = computedHash.SequenceEqual(user.PasswordHash);
            }

            if (!passwordChecksOut)
                return BadRequest("Wrong password");

            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role),
                };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties();

            await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

            return RedirectToAction("Get");
        }
    }
}
