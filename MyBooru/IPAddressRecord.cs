using Microsoft.AspNetCore.Http;
using System;
using System.Net;

namespace MyBooru
{
    public class IPAddressRecord
    {
        public IPAddress RemoteIP { get; }
        public IPAddress LocalIP { get; }
        public DateTime LastRequestTime { get; set; }
        public int NumberOfRequests { get; set; }

        public IPAddressRecord(HttpContext context)
        {
            RemoteIP = context.Connection.RemoteIpAddress;
            LocalIP = context.Connection.LocalIpAddress;
        }

        public override string ToString()
        {
            return $"remote|local : {RemoteIP}|{LocalIP}\nlast|amount : {LastRequestTime}|{NumberOfRequests}";
        }
    }
}
