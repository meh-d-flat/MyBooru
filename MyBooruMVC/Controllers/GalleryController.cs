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
        [Route("")]
        public ActionResult Index(int page = 1)
        {
            return View(model: page);
        }

        [Route("picture")]
        public ActionResult Picture(string id)
        {
            return View(model: id);
        }

        [Route("upload")]
        public ActionResult Upload()
        {
            return View();
        }

        [Route("search")]
        public ActionResult Search(string tags, int page = 1)
        {
            if (tags.EndsWith(","))
                tags = tags.Remove(tags.Length - 1, 1);

            return View(model: (tags, page));
        }
    }
}
