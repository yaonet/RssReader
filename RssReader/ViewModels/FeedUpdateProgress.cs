namespace RssReader.ViewModels
{
    public class FeedUpdateProgress
    {
        public int TotalFeeds { get; set; }
        public int ProcessedFeeds { get; set; }
        public int SuccessfulFeeds { get; set; }
        public int FailedFeeds { get; set; }
        public string? CurrentFeedTitle { get; set; }
        public int Percentage => TotalFeeds > 0 ? (ProcessedFeeds * 100 / TotalFeeds) : 0;
    }
}
