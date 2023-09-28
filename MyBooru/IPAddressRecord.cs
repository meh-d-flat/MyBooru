using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net;

namespace MyBooru
{
    public class IPAddressRecord
    {
        public static List<IPAddressRecord> AllIPRecords { get; }
        public IPAddress RemoteIP { get; }
        public DateTime LastRequestTime { get; set; }
        public int NumberOfRequests { get; set; }

        static IPAddressRecord()
        {
            AllIPRecords = new List<IPAddressRecord>();
        }

        public IPAddressRecord(HttpContext context)
        {
            RemoteIP = context.Connection.RemoteIpAddress;
            LastRequestTime = DateTime.UtcNow;
            NumberOfRequests = 1;
            AllIPRecords.Add(this);
        }

        public override string ToString()
        {
            return $"ip : {RemoteIP}\nlast|amount : {LastRequestTime}|{NumberOfRequests}";
        }
    }
}
