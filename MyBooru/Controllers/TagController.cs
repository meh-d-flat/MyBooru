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
        public IActionResult Get()
        {
            return default;
        }

        [HttpGet]
        public IActionResult Get(string tagName)
        {
            return default;
        }

        [HttpPost]
        public IActionResult Post(string newTag)
        {
            if (!Regex.Match(newTag, @"^[ a-zA-Z0-9]+$").Success)
                return StatusCode(501, "Tag contains illegal characters");
            if (newTag.Length > 32)
                return StatusCode(501, "Tag's too long, be more concise");

            bool added = tagger.Add(newTag);
            if (!added)
                return StatusCode(501, "Something went wrong");

            return Ok();
        }
    }
}
