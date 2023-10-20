using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using static MyBooru.Services.Contracts;

namespace MyBooru.Services
{   
    /// <summary>
    /// A guard from calling implementation/extesion methods on ConcurrentDictionary
    /// </summary>
    public class CachingService : ICachingService
    {
        ConcurrentDictionary<string, JsonResult> _cache;

        public CachingService()
        {
            _cache = new ConcurrentDictionary<string, JsonResult>();
        }

        public bool TryGet(string key, out JsonResult result)
        {
            bool success = _cache.TryGetValue(key, out JsonResult res);
            result = res;
            return success;
        }

        public bool Set(string key, JsonResult val)
        {
            JsonResult newValue = _cache.AddOrUpdate(key, val, (k, v) => val);
            return newValue is not null;
        }

        public bool Remove(string key)
        {
            return _cache.TryRemove(key, out JsonResult _);
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}
