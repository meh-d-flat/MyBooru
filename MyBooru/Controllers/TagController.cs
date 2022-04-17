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
        public IActionResult Get(string tagName)
        {
            var checkResult = InputCheck(tagName);
            if (checkResult != null)
                return checkResult;

            var result = tagger.Get(tagName);
            return new JsonResult(result);
        }

        [HttpPost]
        public IActionResult Post(string newTag)
        {
            var checkResult = InputCheck(newTag);
            if (checkResult != null)
                return checkResult;

            var added = tagger.Add(newTag);
            if (added == null)
                return StatusCode(501, "Something went wrong");

            return Ok();
        }

        IActionResult InputCheck(string text)
        {
            if (text.Length < 3)
                return StatusCode(400, "Tag's too short");
            if (text.Length > 32)
                return StatusCode(400, "Tag's too long");
            if (!Regex.Match(text, @"^[ a-zA-Z0-9]+$").Success)
                return StatusCode(400, "Tag contains illegal characters");

            return null;
        }
    }
}
