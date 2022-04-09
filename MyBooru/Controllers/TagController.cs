using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyBooru.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TagController : ControllerBase
    {
        readonly TagsService tagger;
        readonly char[] illegalChars = "!@#$%^&*()[]{}-_+=~`'\"".ToCharArray();//regex would be better

        public TagController(TagsService tagsService)
        {
            tagger = tagsService;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return default;
        }

        [HttpGet]
        public IActionResult Get(string tagName = "")
        {
            return default;
        }

        [HttpPost]
        public IActionResult Post(string newTag = "")
        {
            if (illegalChars.Any(x => newTag.Contains(x)))
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
