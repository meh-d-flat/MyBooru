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
        readonly Contracts.IDownloadService downloader;

        public MediaController(
            Contracts.ICheckService checkService,
            Contracts.IUploadService uploadService,
            Contracts.IDownloadService downloadService)
        {
            checker = checkService;
            uploader = uploadService;
            downloader = downloadService;

            checker.DBSetup();
        }

        public IActionResult Get()
        {
            return Ok($"{DateTime.Now} Running");
        }

        [HttpGet]
        [Route("download")]
        public IActionResult Download(string id, bool dl = false)
        {
            if (!checker.CheckMediaExists(id))
                return StatusCode(501);

            var result = downloader.Download(id);
            if (result != null)
            {
                var file = new FileContentResult(result.Binary, result.Type);
                if (dl)
                    file.FileDownloadName = result.Name;
                return file;
            }
            else
                return StatusCode(501);
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
