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
        readonly Contracts.IUploadService uploader;

        public MediaController(Contracts.ICheckService checkerService, Contracts.IUploadService uploaderService)
        {
            checker = checkerService;
            uploader = uploaderService;

            checker.DBSetup();
        }

        public IActionResult Get()
        {
            return Ok($"{DateTime.Now} Running");
        }

        [HttpPost]
        [Route("upload")]
        public IActionResult Upload(IFormFile file)
        {
            var result = uploader.UploadOne(file);
            return result == "empty" || result.StartsWith("error")
              ? StatusCode(501, result) : (IActionResult)Ok(result);
        }
    }
}
