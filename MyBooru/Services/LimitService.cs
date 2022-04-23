using System;
using System.Net;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace MyBooru.Services
{
    public class LimitService
    {
        List<IPAddressRecord> all;

        Timer cleanup;

        public List<IPAddressRecord> AllRecords
        {
            get { return all; }
        }

        public LimitService()
        {
            all = new List<IPAddressRecord>();

            cleanup = new Timer(60000);//5400000 - 90 minutes
            cleanup.AutoReset = true;
            cleanup.Elapsed += OldRecordsCleanup;
            cleanup.Start();
        }

        private void OldRecordsCleanup(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("[CLEANING RECORDS]-{0} - Start", DateTime.Now);

            var cleanupTime = DateTime.Now;
            var toRemove = new List<IPAddressRecord>();

            foreach (var record in all)
            {
                if ((cleanupTime - record.LastRequestTime).Minutes > 1)
                {
                    Debug.WriteLine("{1} - cleaning up record:\n{0}", record.ToString(), DateTime.Now);
                    record.NumberOfRequests = 0;
                    toRemove.Add(record);
                }
            }

            foreach (var oldRecord in toRemove)
                all.Remove(oldRecord);

            toRemove = null;

            Debug.WriteLine("[CLEANING RECORDS] - Finish");
        }
    }
}
