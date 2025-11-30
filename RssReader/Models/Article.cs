using System.ComponentModel.DataAnnotations;

namespace RssReader.Models
{
    public class Article
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Content { get; set; }
        public string? Link { get; set; }
        public DateTime PublishDate { get; set; }
        public bool IsRead { get; set; }
        public bool IsFavorite { get; set; }
        public int FeedId { get; set; }
        public Feed? Feed { get; set; }
    }
}
