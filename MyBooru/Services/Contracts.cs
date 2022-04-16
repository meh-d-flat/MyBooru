﻿using Microsoft.AspNetCore.Http;
using MyBooru.Models;
using System;
using System.Collections.Generic;

namespace MyBooru.Services
{
    public class Contracts
    {
        public interface ICheckService
        {
            bool DBSetup();
            bool CheckMediaExists(string id);
        }

        public interface IUploadService
        {
            string UploadOne(IFormFile file);
            List<string> UploadMany(ICollection<IFormFile> files);
        }

        public interface IDownloadService
        {
            Media Download(string id);
        }

        public interface IRemoveService
        {
            string Remove(string id);
        }

        public interface ITagsService
        {
            List<Tag> Get(string name);
            Tag Add(string name);
            List<Tag> AddWithCheck(string tags);
            List<Media> GetByTag(string tags);
        }
    }
}
