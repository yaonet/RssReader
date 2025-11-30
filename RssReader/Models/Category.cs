using System.ComponentModel.DataAnnotations;

namespace RssReader.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<Feed>? Feeds { get; set; }
    }
}
