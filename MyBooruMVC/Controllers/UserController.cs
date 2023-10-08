using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooruMVC.Controllers
{
    [Route("User/")]
    public class UserController : Controller
    {
        public IActionResult Index() => View();

        [Route("details")]
        public IActionResult Details(string username) => View(model: username);

        [Route("login")]
        public IActionResult Login() => View();

        [Route("register")]
        public IActionResult Register() => View();
    }
}
