using System.ComponentModel.DataAnnotations;

namespace RssReader.Models
{
    public class Feed
    {
        public int Id { get; set; }
        public  string? Url { get; set; }
        public  string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
        public string? ImageUrl { get; set; }

        public DateTime LastUpdated { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public ICollection<Article>? Articles { get; set; }
    }
}
