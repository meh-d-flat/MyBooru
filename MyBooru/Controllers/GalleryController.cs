using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Controllers
{
    [Route("Gallery/")]
    public class GalleryController : Controller
    {
        public ActionResult Index()
        {
            //return Ok("hello there!");
            return View();
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
    }
}
