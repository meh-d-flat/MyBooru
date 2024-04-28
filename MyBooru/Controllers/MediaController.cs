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
using Microsoft.Extensions.Caching.Memory;
using MyBooru.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

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
        private readonly CachingService.GalleryCacher _galleryCacher;
        private readonly CachingService.MediaCacher _mediaCacher;
        private readonly IConfiguration _config;

        public MediaController(Contracts.ICheckService checker, Contracts.IDownloadService downloader,
            Contracts.ITagsService tagger, Contracts.IUploadService uploader,
            Contracts.IRemoveService remover, CachingService.GalleryCacher galleryCacher,
            CachingService.MediaCacher mediaCacher, IConfiguration configuration)
        {
            _checker = checker;
            _downloader = downloader;
            _tagger = tagger;
            _uploader = uploader;
            _remover = remover;
            _galleryCacher = galleryCacher;
            _mediaCacher = mediaCacher;
            _config = configuration;
        }
        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct, int page = 1, int reverse = 1)
        {
            var mediasCount = await _checker.MediasCountAsync(ct);

            var q = HttpContext.Request.QueryString.Value;
            q = q == string.Empty ? "?page=1&reverse=1" : q;

            if (_galleryCacher.TryGet(q, out JsonResult res))
                return res;
            else
            {
                res = MakeJsonList(page, reverse, mediasCount, await _downloader.DownloadAsync(page, reverse, ct));
                _galleryCacher.Set(q, res);
            }
            return res;
        }

        [HttpGet, Route("details")]
        public async Task<IActionResult> Details([FromQuery] string id, CancellationToken ct)
        {
            if (!await _checker.CheckMediaExistsAsync(id, ct))
                return NotFound();

            var s = HttpContext.Request.QueryString.Value;

            if (_mediaCacher.TryGet(s, out JsonResult res))
                return res;
            else
            {
                res = new JsonResult(await _downloader.DownloadAsync(id, ct));
                _mediaCacher.Set(s, res);
            }

            return res;
        }

        [HttpPost, Route("addTags"), Authorize(Roles = "User")]
        public async Task<IActionResult> AddTags([FromForm]string id, [FromForm]string tags, CancellationToken ct)
        {
            var h = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "x-query").Value[0];
            _mediaCacher.Remove(h);

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

            var s = HttpContext.Request.QueryString.Value;
            //if (_memoryCache.TryGetValue<List<Media>>(s, out List<Media> result))
            //    return MakeJsonList(page, reverse, mediasCount, result);
            //else
            //{
            //    result = await _tagger.GetMediasByTagsAsync(tags, page, reverse, ct);
            //    _memoryCache.Set<List<Media>>(s, result);
            //}
            var result = await _tagger.GetMediasByTagsAsync(tags, page, reverse, ct);//rewrite to get only ids by tag then go through download
            return MakeJsonList(page, reverse, mediasCount, result);
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

        //The default size is 30000000 bytes (28.6 MB). MaxValue is 4294967295 bytes (4 GB)
        //<requestLimits maxAllowedContentLength="104857600" /> for 100MB in applicationhost.config
        [HttpPost, Route("upload"), Authorize(Roles = "User")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var result = await _uploader.UploadOneAsync(file, HttpContext.User.Identity.Name);
            var response = new JsonResult(new { item = result });
            var isResult = result == "empty" || result.StartsWith("error");

            if (!isResult)
                _galleryCacher.Clear();

            return isResult ? StatusCode(400, response) : (IActionResult)Ok(response);
        }

        [HttpGet, Route("uploadfrom"), Authorize(Roles = "User")]
        public async Task<IActionResult> UploadFrom(string source, CancellationToken ct)
        {
            if (!_config.GetValue<bool>("ExternalFileUploadAllowed"))
                return BadRequest("not allowed");

            if (!Uri.TryCreate(source, UriKind.Absolute, out var givenURI)
                & (givenURI?.Scheme != Uri.UriSchemeHttp || givenURI?.Scheme != Uri.UriSchemeHttps)
                & (givenURI?.HostNameType == UriHostNameType.IPv4 | givenURI?.HostNameType == UriHostNameType.IPv6 | givenURI?.HostNameType == UriHostNameType.Dns)
                & (bool)givenURI?.IsLoopback 
                & (givenURI?.Port != 443 | givenURI?.Port != 80))
                return StatusCode(400, new JsonResult(new { item = "bad url!" }));

            byte[] data;
            HeaderDictionary headers = new HeaderDictionary();
            using (var client = new HttpClient())
            { 
                using (var result = await client.GetAsync(givenURI, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    if (result.Content.Headers.ContentType is null)
                        return StatusCode(400);

                    var s = result.Content.Headers.ContentType.MediaType;//here

                    if (!result.IsSuccessStatusCode || !(s.Contains("image") | s.Contains("video"))) 
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

            var s = HttpContext.Request.QueryString.Value;
            _mediaCacher.Remove(s);
            _galleryCacher.Clear();

            var result = await _remover.RemoveAsync(
                id,
                HttpContext.User.FindFirstValue("uniqueId"),
                HttpContext.User.FindFirstValue(ClaimTypes.Email));
            return result.StartsWith("error")
                ? StatusCode(500, result) : (IActionResult)Ok(result);
        }

        JsonResult MakeJsonList(int page, int reverse, int mediasCount, List<Media> result)
        {
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
    }
}
