namespace RssReader.Services
{
    public class FeedUpdater : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FeedUpdater> _logger;
        private TimeSpan _updateInterval;
        private Timer? _timer;

        public FeedUpdater(
            IServiceProvider serviceProvider,
            ILogger<FeedUpdater> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _updateInterval = TimeSpan.FromMinutes(Settings.DefaultFeedUpdateInterval);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FeedUpdater is starting");

            // Load interval from database
            await LoadUpdateIntervalAsync(stoppingToken);

            var initialDelay = TimeSpan.FromMinutes(2);
            _logger.LogInformation("Waiting {Delay} before first update", initialDelay);
            await Task.Delay(initialDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting scheduled feed update");
                    await UpdateFeedsAsync(stoppingToken);

                    // Reload interval after each update in case it changed
                    await LoadUpdateIntervalAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scheduled feed update");
                }

                _logger.LogInformation("Next feed update scheduled in {Interval}", _updateInterval);
                await Task.Delay(_updateInterval, stoppingToken);
            }

            _logger.LogInformation("FeedUpdater is stopping");
        }

        private async Task LoadUpdateIntervalAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<Settings>();

                var intervalMinutes = await settingsService.GetFeedUpdateIntervalAsync(cancellationToken);
                _updateInterval = TimeSpan.FromMinutes(intervalMinutes);

                _logger.LogInformation("Feed update interval loaded: {Interval} minutes", intervalMinutes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading update interval, using default");
                _updateInterval = TimeSpan.FromMinutes(Settings.DefaultFeedUpdateInterval);
            }
        }

        private async Task UpdateFeedsAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var feedService = scope.ServiceProvider.GetRequiredService<FeedManager>();

            var result = await feedService.UpdateAllFeedsAsync(cancellationToken);

            _logger.LogInformation(
                "Feed update completed: {Total} total, {Success} successful, {Failed} failed",
                result.TotalFeeds, result.SuccessfulUpdates, result.FailedUpdates);

            if (result.Errors.Any())
            {
                foreach (var error in result.Errors.Take(10))
                {
                    _logger.LogWarning("Feed update error: {Error}", error);
                }

                if (result.Errors.Count > 10)
                {
                    _logger.LogWarning("... and {Count} more errors", result.Errors.Count - 10);
                }
            }
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}
