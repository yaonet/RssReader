using RssReader.ViewModels;

namespace RssReader.Services
{
    public class UpdateNotifier
    {
        public event Action? OnCategoriesUpdated;
        public event Action? OnFeedsUpdated;
        public event Action<FeedUpdateProgress>? OnFeedUpdateProgress;

        public void NotifyCategoriesUpdated()
        {
            OnCategoriesUpdated?.Invoke();
        }

        public void NotifyFeedsUpdated()
        {
            OnFeedsUpdated?.Invoke();
        }

        public void NotifyFeedUpdateProgress(FeedUpdateProgress progress)
        {
            OnFeedUpdateProgress?.Invoke(progress);
        }
    }
}
