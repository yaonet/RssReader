namespace RssReader.Services
{
    public class DataUpdateNotificationService
    {
        public event Action? OnCategoriesUpdated;
        public event Action? OnFeedsUpdated;

        public void NotifyCategoriesUpdated()
        {
            OnCategoriesUpdated?.Invoke();
        }

        public void NotifyFeedsUpdated()
        {
            OnFeedsUpdated?.Invoke();
        }
    }
}
