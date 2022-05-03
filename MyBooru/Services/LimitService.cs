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
        List<IPAddressRecord> all;

        Timer cleanup;

        public List<IPAddressRecord> AllRecords
        {
            get { return all; }
        }

        public LimitService(IConfiguration configuration)
        {
            config = configuration;
            all = new List<IPAddressRecord>();

            cleanup = new Timer(config.GetValue<int>("Limiter:CleanupIntervalMs"));//5400000 - 90 minutes
            cleanup.AutoReset = true;
            cleanup.Elapsed += OldRecordsCleanup;
            cleanup.Start();
        }

        private void OldRecordsCleanup(object sender, ElapsedEventArgs e)
        {
            var cleanupTime = DateTime.Now;
            Debug.WriteLine("[CLEANING RECORDS]-{0} - Start", DateTime.Now);

            foreach (var record in all)
            {
                var total = (cleanupTime - record.LastRequestTime).TotalMilliseconds > cleanup.Interval;
                if (total)
                {
                    Debug.WriteLine($"{DateTime.Now} - cleaning up record:\n{record.ToString()}");
                    record.NumberOfRequests = 0;
                }
            }

            Debug.WriteLine("[CLEANING RECORDS] - Finish");
        }
    }
}
