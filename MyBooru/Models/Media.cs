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
        public string Type { get; set; }
        public string Path { get; set; }
        public string Thumb { get; set; }
        public string Uploader { get; set; }
        public int Timestamp { get; set; }

        public List<Tag> Tags { get; set; }
    }
}
