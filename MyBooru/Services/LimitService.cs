using System;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Configuration;

namespace MyBooru.Services
{
    public class LimitService
    {
        private readonly IConfiguration config;
        Timer cleanup;

        public LimitService(IConfiguration configuration)
        {
            config = configuration;
            cleanup = new Timer() { AutoReset = true, Interval = config.GetValue<int>("Limiter:CleanupIntervalMs") };
            cleanup.Elapsed += OldRecordsCleanup;
            cleanup.Start();
        }

        private void OldRecordsCleanup(object sender, ElapsedEventArgs e)
        {
            for (int i = 0; i < IPAddressRecord.AllIPRecords.Count; i++)
            {
                IPAddressRecord.AllIPRecords[i].NumberOfRequests = 0;
            }
        }
    }
}
