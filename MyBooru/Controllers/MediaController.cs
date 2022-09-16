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
using Microsoft.AspNetCore.Authorization;

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
            checker.DBSetupAsync();
        }

        public async Task<IActionResult> Get([FromServices] DownloadService downloader, int page = 1, int reverse = 1)
        {
            var mediasCount = await checker.MediasCountAsync();
            var result = await downloader.DownloadAsync(page, reverse);

            return new JsonResult(new
            {
                page = page,
                prevPage = page - 1 != 0,
                nextPage = mediasCount - (20 * page) > 0,
                total = mediasCount,
                count = result.Count,
                items = result,
                isReversed = reverse
            });
        }

        [HttpGet]
        [Route("details")]
        public async Task<IActionResult> Details([FromServices] DownloadService downloader, [FromQuery] string id)
        {
            if (!await checker.CheckMediaExistsAsync(id))
                return BadRequest();

            var result = await downloader.DownloadAsync(id);
            return new JsonResult(result);
        }

        [HttpGet]
        [Route("addTags")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddTags([FromServices] TagsService tagger, string id, string tags)
        {
            if (!await checker.CheckMediaExistsAsync(id))
                return StatusCode(400);

            //validate the tags
            foreach (var item in tags.Split(","))
            {
                if (!Regex.Match(item, @"[ a-zA-Z0-9]{2,32}$").Success)
                    return BadRequest(new JsonResult(new { bad_tag = item }));
            }

            var newTags = await tagger.AddTagsToMediaAsync(id, tags.ToLower());

            return new JsonResult(new { items = newTags });
        }

        [HttpGet]
        [Route("byTag")]
        public async Task<IActionResult> GetByTags([FromServices] TagsService tagger, string tags, int page = 1, int reverse = 1)
        {
            //validate the tags
            var mediasCount = await tagger.MediasCountAsync(tags);
            var result = await tagger.GetMediasByTagsAsync(tags, page, reverse);//rewrite to get only ids by tag then go through download

            return new JsonResult(new
            {
                page = page,
                prevPage = page - 1 != 0,
                nextPage = mediasCount - (20 * page) > 0,
                total = mediasCount,
                count = result.Count,
                items = result,
                isReversed = reverse
            });
        }

        [HttpGet]
        [Route("download")]
        public async Task<IActionResult> Download([FromServices] DownloadService downloader, string id, bool dl = false)
        {
            if (!await checker.CheckMediaExistsAsync(id))
                return StatusCode(400);

            var result = await downloader.DownloadAsync(id);
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
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Upload([FromServices] UploadService uploader, IFormFile file)
        {
            var result = await uploader.UploadOneAsync(file);
            var response = new JsonResult(new { item = result });
            return result == "empty" || result.StartsWith("error")
              ? StatusCode(501, response) : (IActionResult)Ok(response);
        }

        [HttpGet]
        [Route("uploadfrom")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UploadFrom([FromServices] UploadService uploader, string source)
        {
            byte[] data;
            HeaderDictionary headers = new HeaderDictionary();
            using (var client = new HttpClient())
            {
                using (var result = await client.GetAsync(source, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!result.IsSuccessStatusCode)
                        return StatusCode(400);
                    else
                    {
                        data = await result.Content.ReadAsByteArrayAsync();
                        headers.Add(HeaderNames.ContentType, result.Content.Headers.ContentType.ToString());
                        FormFile formFile = new FormFile((Stream)new MemoryStream(data), 0, data.Length, "file", Path.GetFileName(new Uri(source).AbsolutePath));
                        formFile.Headers = headers;
                        var response = await Upload(uploader, formFile);
                        return response;
                    }
                }
            }
        }

        [Route("remove")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Remove([FromServices] RemoveService remover, string id)
        {
            bool exist = await checker.CheckMediaExistsAsync(id);
            if (!exist)
                return StatusCode(400);

            var result = await remover.RemoveAsync(id);
            return result.StartsWith("error")
                ? StatusCode(501, result) : (IActionResult)Ok(result);
        }
    }
}
