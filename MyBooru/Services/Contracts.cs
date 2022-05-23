using Microsoft.AspNetCore.Http;
using MyBooru.Models;
using System;
using System.Collections.Generic;

namespace MyBooru.Services
{
    public class Contracts
    {
        public interface ICheckService
        {
            int MediasCount();
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
            List<Media> Download(int page);
        }

        public interface IRemoveService
        {
            string Remove(string id);
        }

        public interface ITagsService
        {
            Tag Add(string name);
            List<Tag> SearchTag(string name);
            int MediasCount(string tags);
            List<Media> GetMediasByTags(string tags, int page);
            List<Tag> AddTagsToMedia(string id, string tags);
        }
    }
}
