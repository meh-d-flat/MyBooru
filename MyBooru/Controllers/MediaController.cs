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

        public MediaController(Contracts.ICheckService checkService)
        {
            checker = checkService;
            checker.DBSetup();
        }

        public IActionResult Get()
        {
            return Ok($"{DateTime.Now} Running");
        }

        [HttpGet]
        [Route("download")]
        public IActionResult Download([FromServices]DownloadService downloader, string id, bool dl = false)
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
        public IActionResult Upload([FromServices]UploadService uploader, IFormFile file)
        {
            var result = uploader.UploadOne(file);
            return result == "empty" || result.StartsWith("error")
              ? StatusCode(501, result) : (IActionResult)Ok(result);
        }

        [HttpDelete]
        [Route("remove")]
        public IActionResult Remove([FromServices]RemoveService remover, string id)
        {
            bool exist = checker.CheckMediaExists(id);
            if (!exist)
                return StatusCode(501);

            var result = remover.Remove(id);
            return result.StartsWith("error")
                ? StatusCode(501, result) : (IActionResult)Ok(result);
        }
    }
}
