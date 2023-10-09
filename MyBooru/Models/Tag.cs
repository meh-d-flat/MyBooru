using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string User { get; set; }
        public int DateTime { get; set; }
    }
}
