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
        private readonly int requestsIntevalMs;


        public RequestLimitMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            requestsIntevalMs = config.GetValue<int>("Limiter:RequestsIntevalMs");
            numOfRequests = config.GetValue<int>("Limiter:RequestsNumber");
        }

        public async Task Invoke(HttpContext context, LimitService limiter)
        {
            var ipString = context.Connection.LocalIpAddress.ToString();
            var recordExists = limiter.AllRecords.Any(x => x.LocalIP.ToString() == ipString);

            var record = recordExists
                ? limiter.AllRecords.Find(x => x.LocalIP.ToString() == ipString)
                : new IPAddressRecord(context);

            if (!recordExists)
                limiter.AllRecords.Add(record);
            
            if (record.NumberOfRequests > numOfRequests)
                await TooManyRequestsResponse(context);

            if ((DateTime.Now - record.LastRequestTime).Milliseconds < requestsIntevalMs)
                await TooManyRequestsResponse(context);
            
            record.NumberOfRequests++;
            record.LastRequestTime = DateTime.Now;

            //inverse this
            if (record.NumberOfRequests <= 25)
                await _next(context);

        }

        async Task TooManyRequestsResponse(HttpContext context)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Too many requests!");
        }
    }
}
