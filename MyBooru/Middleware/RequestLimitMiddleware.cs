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
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class RequestLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly int numOfRequests;


        public RequestLimitMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            numOfRequests = config.GetValue<int>("Limiter:RequestsNumber");
        }

        public async Task Invoke(HttpContext context, LimitService limiter)
        {
            var isNew = IPAddressRecord.AllIPRecords.Any(x => x.RemoteIP.Equals(context.Connection.RemoteIpAddress));
            var ipRecord = isNew
                ? IPAddressRecord.AllIPRecords.Find(x => x.RemoteIP.Equals(context.Connection.RemoteIpAddress))
                : new IPAddressRecord(context);
            
            ipRecord.LastRequestTime = DateTime.UtcNow;
            ipRecord.NumberOfRequests++;

            if (ipRecord.NumberOfRequests > numOfRequests)
                await TooManyRequestsResponse(context);
            else
                await _next(context);
        }

        async Task TooManyRequestsResponse(HttpContext context)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Too many requests!");
        }
    }
}
