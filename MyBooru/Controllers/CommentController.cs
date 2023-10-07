using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyBooru.Services;
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
        [HttpGet]
        [Route("byMedia")]
        public async Task<IActionResult> Get([FromServices] CommentService commService, string id, CancellationToken ct)
        {
            var result = await commService.GetCommentsOnMediaAsync(id, ct);
            return result == null
                ? NotFound()
                : Ok(new JsonResult(new
                {
                    items = result
                }));
        }

        [HttpGet]
        [Route("mine")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Get([FromServices] CommentService commService, CancellationToken ct)
        {
            var result = await commService.GetMyCommentsAsync(
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

        [HttpPost]
        [Authorize(Roles = "User")]
        [Route("post")]
        public async Task<IActionResult> Post([FromServices] CommentService commService, [FromForm] string commentText, [FromForm] string hash)
        {
            var result = await commService.PostCommentAsync(HttpContext.User.Identity.Name, commentText, hash);
            return result > 0 ? Ok(result) : StatusCode(500);
        }

        [HttpDelete]
        [Route("remove")]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Delete([FromServices] CommentService commService, int id)
        {
            var result = await commService.RemoveCommentAsync(
                id,
                HttpContext.User.FindFirstValue("uniqueId"),
                HttpContext.User.FindFirstValue(ClaimTypes.Email));
            return result > 0 ? Ok(result) : StatusCode(500);
        }
    }
}
