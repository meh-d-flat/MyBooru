using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using MyBooru.Services;

namespace MyBooru.Middleware
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class RequestLimitMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestLimitMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, LimitService limiter)
        {
            IPAddressRecord olderVisitor = null;
            IPAddressRecord freshVisitor = new IPAddressRecord(context.Connection.RemoteIpAddress, context.Connection.LocalIpAddress, DateTime.Now);

            if (!CheckIsVisitorFresh(limiter, freshVisitor.LocalIP, freshVisitor.RemoteIP))
                limiter.AllRecords.Add(freshVisitor);
            else
            {
                olderVisitor = limiter.AllRecords.Find(x => x.LocalIP.ToString() == freshVisitor.LocalIP.ToString());
                olderVisitor.NumberOfRequests++;

                if (olderVisitor.NumberOfRequests > 25)
                    await TooManyRequestsResponse(context);

                if ((DateTime.Now - olderVisitor.LastRequestTime).Milliseconds < 70)
                    await TooManyRequestsResponse(context);

                olderVisitor.LastRequestTime = DateTime.Now;
            }

            if (olderVisitor == null || olderVisitor.NumberOfRequests <= 25)
                await _next(context);
        }

        bool CheckIsVisitorFresh(LimitService limiter, IPAddress localIP, IPAddress remoteIP)
        {
            return limiter.AllRecords.Any(x => x.LocalIP.ToString() == localIP.ToString());
        }

        async Task TooManyRequestsResponse(HttpContext context)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Too many requests!");
        }
    }
}
