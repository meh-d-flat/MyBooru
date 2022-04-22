using System;
using System.Net;

namespace MyBooru
{
    public class IPAddressRecord
    {
        public IPAddress RemoteIP { get; }
        public IPAddress LocalIP { get; }
        public DateTime InitialRequestTime { get; }
        public DateTime LastRequestTime { get; set; }
        public int NumberOfRequests { get; set; }

        public IPAddressRecord(IPAddress remote, IPAddress local, DateTime initial)
        {
            RemoteIP = remote;
            LocalIP = local;
            InitialRequestTime = initial;
        }

        public override string ToString()
        {
            return $"remote|local : {RemoteIP}|{LocalIP}\ninitial|last|amount : {InitialRequestTime}|{LastRequestTime}|{NumberOfRequests}";
        }
    }
}
