using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MyBooru.Models;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace MyBooru.Services
{
    public class Contracts
    {
        public interface ICheckService
        {
            Task<int> MediasCountAsync(CancellationToken ct);
            Task<bool> DBSetupAsync();
            Task<bool> CheckMediaExistsAsync(string id, CancellationToken ct);
        }

        public interface IUploadService
        {
            Task<string> UploadOneAsync(IFormFile file, string username);
            Task<List<string>> UploadManyAsync(List<IFormFile> files, string username);
        }

        public interface IDownloadService
        {
            Task<Media> DownloadAsync(string id, CancellationToken ct);
            Task<List<Media>> DownloadAsync(int page, int reverse, CancellationToken ct);
        }

        public interface IRemoveService
        {
            Task<string> RemoveAsync(string id);
        }

        public interface ITagsService
        {
            Task <Tag> AddTagAsync(string name);
            Task<List<Tag>> SearchTagAsync(string name, CancellationToken ct);
            Task<int> MediasCountAsync(string tags, CancellationToken ct);
            Task<List<Media>> GetMediasByTagsAsync(string tags, int page, int reverse, CancellationToken ct);
            Task<List<Tag>> AddTagsToMediaAsync(string id, string tags);
        }

        public interface IUserService
        {
            Task<User> PersistUserAsync(string username, string password, string email, CancellationToken ct);
            Task<User> GetUserAsync(string username, CancellationToken ct);
            Task<bool> CheckEmailAsync(string email);
            Task<bool> CheckUsernameAsync(string username);
            Task<bool> CheckPasswordAsync(string username, string password, CancellationToken ct);
            Task<List<Ticket>> GetUserSessionsAsync(string username, CancellationToken ct);
            Task<bool> CloseUserSessionAsync(string sessionId, string email);
        }

        public interface IQueryService
        {
            Task<T> QueryTheDbAsync<T>(Func<SQLiteCommand, Task<T>> f, string query);
        }
    }
}
