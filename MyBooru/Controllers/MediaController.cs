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
using static MyBooru.Services.Contracts;
using System.Text.RegularExpressions;

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

        public IActionResult Get([FromServices] DownloadService downloader, int page = 1)
        {
            var mediasCount = checker.MediasCount();
            var results = downloader.Download(page);
            return new JsonResult(new
            { 
                page = page,
                prevPage = page - 1 != 0,
                nextPage = mediasCount - (20 * page) > 0,
                total = mediasCount,
                count = results.Count,
                items = results
            });
        }

        [HttpGet]
        [Route("details")]
        public IActionResult Details([FromServices] DownloadService downloader, [FromQuery]string id)
        {
            if (!checker.CheckMediaExists(id))
                return BadRequest();

            var result = downloader.Download(id);
            return new JsonResult(result);
        }

        [HttpGet]
        [Route("addTags")]
        public IActionResult AddTags([FromServices] TagsService tagger, string id, string tags)
        {
            if (!checker.CheckMediaExists(id))
                return StatusCode(400);

            foreach (var item in tags.Split(","))
            {
                if(!Regex.Match(item, @"[ a-zA-Z0-9]{3,32}$").Success)
                    return BadRequest(new JsonResult(new { bad_tag = item }));
            }

            var newTags = tagger.AddTagsToMedia(id, tags);

            return new JsonResult(new { items = newTags });
        }

        [HttpGet]
        [Route("byTag")]
        public IActionResult GetByTags([FromServices] TagsService tagger, string tags, int page = 1)
        {
            var mediasCount = tagger.MediasCount(tags);
            var result = tagger.GetMediasByTags(tags, page);//rewrite to get only ids by tag then go through download
            return new JsonResult(new
            { 
                page = page,
                prevPage = page - 1 != 0,
                nextPage = mediasCount - (20 * page) > 0,
                total = mediasCount,
                count = result.Count,
                items = result 
            });
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
                //var file = new FileContentResult((result.Binary, result.Type);
                var file = new PhysicalFileResult(result.Path, result.Type);
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
            var response = new JsonResult(new { item = result });
            return result == "empty" || result.StartsWith("error")
              ? StatusCode(501, response) : (IActionResult)Ok(response);
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
