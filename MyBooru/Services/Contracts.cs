using Microsoft.AspNetCore.Http;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            List<Media> Download(int page, int reverse);
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
            List<Media> GetMediasByTags(string tags, int page, int reverse);
            List<Tag> AddTagsToMedia(string id, string tags);
        }

        public interface IUserService
        {
            Task<User> PersistUserAsync(string username, string password, string email);
            Task<User> GetUserAsync(string username);
            Task<bool> CheckEmailAsync(string email);
            Task<bool> CheckUsernameAsync(string username);
            Task<bool> CheckPasswordAsync(string username, string password);
        }
    }
}
