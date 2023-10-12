using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MyBooru.Services;
using Microsoft.Extensions.Configuration;

namespace MyBooru.Middleware
{
    // and let's not forget about forwarding headers if we use proxy
    public class OriginRefererCheckMiddleware
    {
        private readonly IConfiguration config;
        private readonly RequestDelegate _next;

        public OriginRefererCheckMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            this.config = config;
        }

        public async Task Invoke(HttpContext context)
        {            
            var hasConsumerOrigin = context.Request.Headers.TryGetValue("Origin", out var originCollection);
            var hasConsumerReferer = context.Request.Headers.TryGetValue("Referer", out var refererCollection);

            if (!hasConsumerOrigin || !hasConsumerReferer)
            {
                context.Response.StatusCode = 400;//or 412
                return;
            }

            if (originCollection.Count > 1 || refererCollection.Count > 1)
            {
                context.Response.StatusCode = 400;
                return;
            }

            string consumerOrigin = originCollection[0];
            string consumerReferer = refererCollection[0]?.TrimEnd('/');

            if(consumerOrigin == null ||  consumerReferer == null)
            {
                context.Response.StatusCode = 400;
                return;
            }

            if (!config.GetSection("ApiConsumerAddressPort").Get<string[]>().Any(x => x == consumerOrigin & x == consumerReferer))
            {
                context.Response.StatusCode = 400;
                return;
            }

            await _next(context);
        }
    }
}
