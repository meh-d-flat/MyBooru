using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyBooru.Services;
using System.Net.Http;
using Microsoft.Net.Http.Headers;
using System.IO;

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
        [Route("addTags")]
        public IActionResult AddTags([FromServices] TagsService tagger, string id, string tags)
        {
            if (!checker.CheckMediaExists(id))
                return StatusCode(400);

            var tagsList = tagger.AddWithCheck(tags);
            tagger.AddToMedia(id, tagsList);

            return Ok();
        }

        [HttpGet]
        [Route("byTag")]
        public IActionResult GetByTags([FromServices] TagsService tagger, string tags)
        {
            var result = tagger.GetByTag(tags);
            return new JsonResult(result);
        }

        [HttpGet]
        [Route("download")]
        public IActionResult Download([FromServices] DownloadService downloader, string id, bool dl = false)
        {
            if (!checker.CheckMediaExists(id))
                return StatusCode(400);

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
        public IActionResult Upload([FromServices] UploadService uploader, IFormFile file)
        {
            var result = uploader.UploadOne(file);
            return result == "empty" || result.StartsWith("error")
              ? StatusCode(501, result) : (IActionResult)Ok(result);
        }

        [HttpGet]
        [Route("uploadfrom")]
        public IActionResult UploadFrom([FromServices] UploadService uploader, string source)
        {
            byte[] data;
            HeaderDictionary headers = new HeaderDictionary();
            using (var client = new HttpClient())
            {
                using (var result = client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead).Result)
                {
                    if (!result.IsSuccessStatusCode)
                        return StatusCode(400);
                    else
                    {
                        data = result.Content.ReadAsByteArrayAsync().Result;
                        headers.Add(HeaderNames.ContentType, result.Content.Headers.ContentType.ToString());
                        FormFile formFile = new FormFile((Stream)new MemoryStream(data), 0, data.Length, "file", Path.GetFileName(new Uri(source).AbsolutePath));
                        formFile.Headers = headers;
                        var response = Upload(uploader, formFile);
                        return response;
                    }
                }
            }
        }

        [HttpDelete]
        [Route("remove")]
        public IActionResult Remove([FromServices] RemoveService remover, string id)
        {
            bool exist = checker.CheckMediaExists(id);
            if (!exist)
                return StatusCode(400);

            var result = remover.Remove(id);
            return result.StartsWith("error")
                ? StatusCode(501, result) : (IActionResult)Ok(result);
        }
    }
}
