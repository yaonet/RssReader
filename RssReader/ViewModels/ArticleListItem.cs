namespace RssReader.ViewModels
{
    public class ArticleListItem
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Link { get; set; }
        public DateTime PublishDate { get; set; }
        public bool IsRead { get; set; }
        public bool IsFavorite { get; set; }
        public int FeedId { get; set; }
        
        public string? FeedTitle { get; set; }
        public string? FeedLink { get; set; }
        public string? FeedImageUrl { get; set; }
        public string? CategoryName { get; set; }
    }
}
