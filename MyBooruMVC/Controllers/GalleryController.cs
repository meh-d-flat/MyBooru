﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooruMVC.Controllers
{
    [Route("Gallery/")]
    public class GalleryController : Controller
    {
        public ActionResult Index()
        {
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

        [Route("search")]
        public ActionResult Search(string tags)
        {
            return View(model: tags);
        }
    }
}