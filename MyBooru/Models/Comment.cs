using Microsoft.CodeAnalysis;

namespace MyBooru.Models
{
    public class Comment
    {
        public int ID { get; set; }
        public string Text { get; set; }
        public string User { get; set; }
        public int Timestamp { get; set; }
        public string MediaID { get; set; }

        public Media Media { get; set; }
    }
}
