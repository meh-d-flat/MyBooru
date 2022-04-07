using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyBooru.Services;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        readonly Contracts.ICheckService checker;

        public MediaController(Contracts.ICheckService checkerService)
        {
            checker = checkerService;
        }

        public IActionResult Get()
        {
            return Ok($"{DateTime.Now} Running");
        }
    }
}
