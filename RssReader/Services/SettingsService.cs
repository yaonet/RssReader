using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RssReader.Data;
using RssReader.Models;

namespace RssReader.Services
{
    /// <summary>
    /// Service for managing application settings stored in the database
    /// </summary>
    public class SettingsService
    {
        private readonly IDbContextFactory<RssReaderContext> _dbContextFactory;
        private readonly ILogger<SettingsService> _logger;
        private readonly IMemoryCache _cache;
        
        public const string FeedUpdateIntervalKey = "FeedUpdate.IntervalMinutes";
        public const int DefaultFeedUpdateInterval = 60;
        
        private const string CacheKeyPrefix = "AppSetting_";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public SettingsService(
            IDbContextFactory<RssReaderContext> dbContextFactory,
            ILogger<SettingsService> logger,
            IMemoryCache cache)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Get setting value by key
        /// </summary>
        public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{key}";
            
            if (_cache.TryGetValue<string>(cacheKey, out var cachedValue))
            {
                _logger.LogDebug("Setting {Key} retrieved from cache", key);
                return cachedValue;
            }

            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var setting = await context.AppSetting
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
                
                var value = setting?.Value;
                
                if (value != null)
                {
                    _cache.Set(cacheKey, value, CacheDuration);
                    _logger.LogDebug("Setting {Key} cached for {Duration}", key, CacheDuration);
                }
                
                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting setting {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Get setting value as integer
        /// </summary>
        public async Task<int?> GetSettingAsIntAsync(string key, CancellationToken cancellationToken = default)
        {
            var value = await GetSettingAsync(key, cancellationToken);
            if (value != null && int.TryParse(value, out var intValue))
            {
                return intValue;
            }
            return null;
        }

        /// <summary>
        /// Set or update a setting value
        /// </summary>
        public async Task<bool> SetSettingAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                var setting = await context.AppSetting
                    .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

                if (setting == null)
                {
                    setting = new AppSetting
                    {
                        Key = key,
                        Value = value,
                        Description = description,
                        LastUpdated = DateTime.UtcNow
                    };
                    context.AppSetting.Add(setting);
                    _logger.LogInformation("Created new setting: {Key} = {Value}", key, value);
                }
                else
                {
                    setting.Value = value;
                    setting.LastUpdated = DateTime.UtcNow;
                    if (description != null)
                    {
                        setting.Description = description;
                    }
                    _logger.LogInformation("Updated setting: {Key} = {Value}", key, value);
                }

                await context.SaveChangesAsync(cancellationToken);
                
                // Invalidate cache
                var cacheKey = $"{CacheKeyPrefix}{key}";
                _cache.Remove(cacheKey);
                _logger.LogDebug("Cache invalidated for setting {Key}", key);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting {Key} = {Value}", key, value);
                return false;
            }
        }

        /// <summary>
        /// Get feed update interval in minutes
        /// </summary>
        public async Task<int> GetFeedUpdateIntervalAsync(CancellationToken cancellationToken = default)
        {
            var interval = await GetSettingAsIntAsync(FeedUpdateIntervalKey, cancellationToken);
            return interval ?? DefaultFeedUpdateInterval;
        }

        /// <summary>
        /// Set feed update interval in minutes
        /// </summary>
        public async Task<bool> SetFeedUpdateIntervalAsync(int minutes, CancellationToken cancellationToken = default)
        {
            if (minutes < 1 || minutes > 1440)
            {
                _logger.LogWarning("Invalid feed update interval: {Minutes} minutes", minutes);
                return false;
            }

            return await SetSettingAsync(
                FeedUpdateIntervalKey, 
                minutes.ToString(), 
                "Feed update interval in minutes", 
                cancellationToken);
        }

        /// <summary>
        /// Get all settings
        /// </summary>
        public async Task<List<AppSetting>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                return await context.AppSetting
                    .AsNoTracking()
                    .OrderBy(s => s.Key)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all settings");
                return new List<AppSetting>();
            }
        }

        /// <summary>
        /// Initialize default settings if they don't exist
        /// </summary>
        public async Task InitializeDefaultSettingsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var currentInterval = await GetSettingAsync(FeedUpdateIntervalKey, cancellationToken);
                if (currentInterval == null)
                {
                    await SetFeedUpdateIntervalAsync(DefaultFeedUpdateInterval, cancellationToken);
                    _logger.LogInformation("Initialized default feed update interval: {Minutes} minutes", DefaultFeedUpdateInterval);
                }
                else
                {
                    _logger.LogInformation("Settings already initialized, feed update interval: {Interval}", currentInterval);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing default settings");
            }
        }
    }
}
