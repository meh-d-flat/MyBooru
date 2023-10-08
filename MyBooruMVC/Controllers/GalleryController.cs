using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooruMVC.Controllers
{
    [Route("Gallery/")]
    public class GalleryController : Controller
    {
        static readonly char[] trail = { ',' };

        [Route("")]
        public ActionResult Index(int page = 1, int reverse = 1) => View(model: (page, reverse));

        [Route("picture")]
        public ActionResult Picture(string id) => View(model: id);

        [Route("upload")]
        public ActionResult Upload() =>  View();

        [Route("search")]
        public ActionResult Search(string tags, int page = 1, int reverse = 1)
        {
            return View(model: (tags.TrimEnd(trail), page, reverse));
        }
    }
}
