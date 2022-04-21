using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MyBooru.Models
{
    public class Media
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }
        //public int Size { get; set; }
        public string Type { get; set; }
        //public byte[] Binary { get; set; }
        public string Path { get; set; }

        public List<Tag> Tags { get; set; }
    }
}
