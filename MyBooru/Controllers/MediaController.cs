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
using System.Threading;
using System.Security.Claims;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        readonly Contracts.ICheckService _checker;
        readonly Contracts.IDownloadService _downloader;
        readonly Contracts.ITagsService _tagger;
        readonly Contracts.IUploadService _uploader;
        readonly Contracts.IRemoveService _remover;

        public MediaController(Contracts.ICheckService checker, Contracts.IDownloadService downloader,
            Contracts.ITagsService tagger, Contracts.IUploadService uploader, Contracts.IRemoveService remover)
        {
            _checker = checker;
            _downloader = downloader;
            _tagger = tagger;
            _uploader = uploader;
            _remover = remover;
        }
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct, int page = 1, int reverse = 1)
        {
            var mediasCount = await _checker.MediasCountAsync(ct);
            var result = await _downloader.DownloadAsync(page, reverse, ct);

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

        [HttpGet, Route("details")]
        public async Task<IActionResult> Details([FromQuery] string id, CancellationToken ct)
        {
            if (!await _checker.CheckMediaExistsAsync(id, ct))
                return NotFound();

            var result = await _downloader.DownloadAsync(id, ct);
            return new JsonResult(result);
        }

        [HttpGet, Route("addTags"), Authorize(Roles = "User")]
        public async Task<IActionResult> AddTags(string id, string tags, CancellationToken ct)
        {
            if (!await _checker.CheckMediaExistsAsync(id, ct))
                return StatusCode(400);

            var delimited = tags.Split(",");
            for (int i = 0; i < delimited.Length; i++)
            {
                if (!Regex.Match(delimited[i], @"[ a-zA-Z0-9]{2,32}$").Success)
                    return BadRequest(new JsonResult(new { bad_tag = delimited[i] }));
            }

            var newTags = await _tagger.AddTagsToMediaAsync(id, tags.ToLower(), HttpContext.User.Identity.Name);

            return new JsonResult(new { items = newTags });
        }

        [HttpGet, Route("byTag")]
        public async Task<IActionResult> GetByTags(string tags, CancellationToken ct, int page = 1, int reverse = 1)
        {
            //validate the tags
            var mediasCount = await _tagger.MediasCountAsync(tags, ct);
            var result = await _tagger.GetMediasByTagsAsync(tags, page, reverse, ct);//rewrite to get only ids by tag then go through download

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

        [HttpGet, Route("download")]
        public async Task<IActionResult> Download(string id, CancellationToken ct, bool dl = false)
        {
            if (!await _checker.CheckMediaExistsAsync(id, ct))
                return NotFound();

            var result = await _downloader.DownloadAsync(id, ct);
            if (result != null)
            {
                //var file = new FileContentResult((result.Binary, result.Type);
                var file = new PhysicalFileResult(result.Path, result.Type);
                if (dl)
                    file.FileDownloadName = result.Name;
                return file;
            }
            else
                return StatusCode(400);
        }

        [HttpPost, Route("upload"), Authorize(Roles = "User")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var result = await _uploader.UploadOneAsync(file, HttpContext.User.Identity.Name);
            var response = new JsonResult(new { item = result });
            return result == "empty" || result.StartsWith("error")
              ? StatusCode(400, response) : (IActionResult)Ok(response);
        }

        [HttpGet, Route("uploadfrom"), Authorize(Roles = "User")]
        public async Task<IActionResult> UploadFrom(string source)
        {
            if (!Uri.TryCreate(source, UriKind.Absolute, out var givenURI) && !(givenURI?.Scheme == Uri.UriSchemeHttp || givenURI?.Scheme == Uri.UriSchemeHttps))
                return StatusCode(400, new JsonResult(new { item = "bad url!" }));

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
                        var response = await Upload(formFile);
                        return response;
                    }
                }
            }
        }

        [HttpDelete, Route("remove"), Authorize(Roles = "User")]
        public async Task<IActionResult> Remove(string id, CancellationToken ct)
        {
            bool exist = await _checker.CheckMediaExistsAsync(id, ct);
            if (!exist)
                return StatusCode(400);

            var result = await _remover.RemoveAsync(
                id,
                HttpContext.User.FindFirstValue("uniqueId"),
                HttpContext.User.FindFirstValue(ClaimTypes.Email));
            return result.StartsWith("error")
                ? StatusCode(500, result) : (IActionResult)Ok(result);
        }
    }
}
