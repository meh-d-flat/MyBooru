﻿using Microsoft.AspNetCore.Http;
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
            Task<int> MediasCountAsync();
            Task<bool> DBSetupAsync();
            Task<bool> CheckMediaExistsAsync(string id);
        }

        public interface IUploadService
        {
            Task<string> UploadOneAsync(IFormFile file);
            Task<List<string>> UploadManyAsync(ICollection<IFormFile> files);
        }

        public interface IDownloadService
        {
            Task<Media> DownloadAsync(string id);
            Task<List<Media>> DownloadAsync(int page, int reverse);
        }

        public interface IRemoveService
        {
            Task<string> RemoveAsync(string id);
        }

        public interface ITagsService
        {
            Task <Tag> AddTagAsync(string name);
            Task<List<Tag>> SearchTagAsync(string name);
            Task<int> MediasCountAsync(string tags);
            Task<List<Media>> GetMediasByTagsAsync(string tags, int page, int reverse);
            Task<List<Tag>> AddTagsToMediaAsync(string id, string tags);
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
