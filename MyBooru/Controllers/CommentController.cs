using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MyBooru.Services;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using static MyBooru.Services.Contracts;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentController : ControllerBase
    {
        readonly Contracts.ICommentService _commService;
        private readonly IMemoryCache _memoryCache;

        public CommentController(Contracts.ICommentService commService, IMemoryCache memoryCache)
        {
            _commService = commService;
            _memoryCache = memoryCache;
        }

        [HttpGet, Route("byMedia")]
        public async Task<IActionResult> Get(string id, CancellationToken ct)
        {
            var result = await _commService.GetCommentsOnMediaAsync(id, ct);
            return result == null
                ? NotFound()
                : Ok(new JsonResult(new
                {
                    items = result
                }));
        }

        [HttpGet, Route("mine"), Authorize(Roles = "User")]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var result = await _commService.GetMyCommentsAsync(
                HttpContext.User.FindFirstValue("uniqueId"),
                HttpContext.User.FindFirstValue(ClaimTypes.Email),
                ct);
            return result == null
                ? BadRequest()
                : Ok(new JsonResult(new
                {
                    items = result
                }));
        }

        [HttpGet, Route("byId")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var result = await _commService.GetCommentAsync(id, ct);
            return result != null
                ? Ok(result)
                : NotFound() ;
        }

        [HttpPost, Authorize(Roles = "User"), Route("post")]
        public async Task<IActionResult> Post([FromForm] string commentText, [FromForm] string hash)
        {
            var h = HttpContext.Request.Headers.FirstOrDefault(x => x.Key == "x-query").Value[0];
            _memoryCache.Remove(h);

            var result = await _commService.PostCommentAsync(HttpContext.User.Identity.Name, commentText, hash);
            return result > 0 ? Ok(result) : StatusCode(500);
        }

        [HttpDelete, Route("remove"), Authorize(Roles = "User")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _commService.RemoveCommentAsync(
                id,
                HttpContext.User.FindFirstValue("uniqueId"),
                HttpContext.User.FindFirstValue(ClaimTypes.Email));
            return result > 0 ? Ok(result) : StatusCode(500);
        }
    }
}
