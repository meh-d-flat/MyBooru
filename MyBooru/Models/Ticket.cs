using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Models
{
    public class Ticket
    {
        public string ID { get; set; }
        public string Username { get; set; }
        public byte[] Value { get; set; }
        public int LastActivity { get; set; }
        public string UserAgent { get; set; }
        public string IP { get; set; }
    }
}
