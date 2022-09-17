using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyBooru.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TagController : ControllerBase
    {
        readonly Contracts.ITagsService tagger;

        public TagController(Contracts.ITagsService tagsService)
        {
            tagger = tagsService;
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Get(string tagName)
        {
            if (!InputCheck(tagName))
                return StatusCode(400, "Bad tag");

            var resultCollection = await tagger.SearchTagAsync(tagName);
            var result = resultCollection.Select(x => x.Name);
            return new JsonResult(result);
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Post(string newTag)
        {
            if (!InputCheck(newTag))
                return StatusCode(400, "Bad tag");

            var added = await tagger.AddTagAsync(newTag);
            if (added == null)
                return StatusCode(501, "Something went wrong");

            return Ok();
        }

        bool InputCheck(string text)
        {
            return Regex.Match(text, @"[ a-zA-Z0-9]{2,32}$").Success;
        }
    }
}
