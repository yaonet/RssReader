using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using RssReader.Data;
using RssReader.Models;

namespace RssReader.Services
{
    public class FeedService
    {
        private readonly IHttpClientFactory? _httpClientFactory;
        private readonly IDbContextFactory<RssReaderContext>? _dbContextFactory;
        private readonly DataUpdateNotificationService? _notificationService;
        private readonly ILogger<FeedService> _logger;

        public FeedService(
            IHttpClientFactory httpClientFactory,
            IDbContextFactory<RssReaderContext> dbContextFactory,
            DataUpdateNotificationService notificationService,
            ILogger<FeedService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _dbContextFactory = dbContextFactory;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task<Feed> CreateFeedFromUrlAsync(string url, int categoryId, int? maxArticles = null)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                Async = true
            };

            using var reader = XmlReader.Create(url, settings);
            SyndicationFeed syndicationFeed = SyndicationFeed.Load(reader);

            var newFeed = new Feed
            {
                Title = syndicationFeed.Title.Text,
                Url = url,
                Description = syndicationFeed.Description?.Text,
                Link = syndicationFeed.Links.FirstOrDefault()?.Uri.ToString(),
                ImageUrl = await GetImageUrlAsync(syndicationFeed),
                LastUpdated = syndicationFeed.LastUpdatedTime.UtcDateTime != default
                    ? syndicationFeed.LastUpdatedTime.UtcDateTime
                    : DateTime.UtcNow,
                CategoryId = categoryId,
                Articles = new List<Article>()
            };

            var items = maxArticles.HasValue 
                ? syndicationFeed.Items.Take(maxArticles.Value) 
                : syndicationFeed.Items;

            foreach (var item in items)
            {
                var article = CreateArticleFromSyndicationItem(item, newFeed);
                newFeed.Articles.Add(article);
            }

            return newFeed;
        }

        public async Task<FeedUpdateResult> UpdateAllFeedsAsync(CancellationToken cancellationToken = default)
        {
            EnsureDependenciesInitialized();

            var result = new FeedUpdateResult();

            try
            {
                await using var context = await _dbContextFactory!.CreateDbContextAsync(cancellationToken);
                var feeds = await context.Feed
                    .OrderBy(f => f.LastUpdated)
                    .ToListAsync(cancellationToken);

                result.TotalFeeds = feeds.Count;
                _logger!.LogInformation("Starting update of {TotalFeeds} feeds", result.TotalFeeds);

                foreach (var feed in feeds)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Feed update cancelled");
                        break;
                    }

                    try
                    {
                        await UpdateSingleFeedAsync(context, feed, cancellationToken);
                        result.SuccessfulUpdates++;
                    }
                    catch (Exception ex)
                    {
                        result.FailedUpdates++;
                        result.Errors.Add($"Feed '{feed.Title}': {ex.Message}");
                        _logger.LogError(ex, "Failed to update feed {FeedId} ({FeedTitle})", feed.Id, feed.Title);
                    }
                }

                _logger.LogInformation(
                    "Feed update completed. Total: {Total}, Success: {Success}, Failed: {Failed}",
                    result.TotalFeeds, result.SuccessfulUpdates, result.FailedUpdates);

                if (result.SuccessfulUpdates > 0)
                {
                    _notificationService!.NotifyFeedsUpdated();
                }
            }
            catch (Exception ex)
            {
                _logger!.LogError(ex, "Error during feed update process");
                result.Errors.Add($"Critical error: {ex.Message}");
            }

            return result;
        }

        public async Task<bool> UpdateSingleFeedByIdAsync(int feedId, CancellationToken cancellationToken = default)
        {
            EnsureDependenciesInitialized();

            try
            {
                await using var context = await _dbContextFactory!.CreateDbContextAsync(cancellationToken);
                var feed = await context.Feed.FindAsync(new object[] { feedId }, cancellationToken);

                if (feed == null)
                {
                    _logger!.LogWarning("Feed with ID {FeedId} not found", feedId);
                    return false;
                }

                await UpdateSingleFeedAsync(context, feed, cancellationToken);
                _notificationService!.NotifyFeedsUpdated();
                return true;
            }
            catch (Exception ex)
            {
                _logger!.LogError(ex, "Error updating feed {FeedId}", feedId);
                return false;
            }
        }

        private void EnsureDependenciesInitialized()
        {
            if (_httpClientFactory == null || _dbContextFactory == null || _notificationService == null || _logger == null)
            {
                throw new InvalidOperationException("FeedService was not initialized with required dependencies. Use the constructor with all parameters.");
            }
        }

        private async Task UpdateSingleFeedAsync(RssReaderContext context, Feed feed, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(feed.Url))
            {
                _logger?.LogWarning("Feed {FeedId} has no URL, skipping", feed.Id);
                return;
            }

            _logger?.LogDebug("Updating feed {FeedId} ({FeedTitle}) from {FeedUrl}", feed.Id, feed.Title, feed.Url);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                Async = true
            };

            using var reader = XmlReader.Create(feed.Url, settings);
            SyndicationFeed syndicationFeed = SyndicationFeed.Load(reader);

            feed.Title = syndicationFeed.Title.Text;
            feed.Description = syndicationFeed.Description?.Text;
            feed.Link = syndicationFeed.Links.FirstOrDefault()?.Uri.ToString();
            feed.LastUpdated = syndicationFeed.LastUpdatedTime.UtcDateTime != default
                ? syndicationFeed.LastUpdatedTime.UtcDateTime
                : DateTime.UtcNow;

            var existingArticleLinks = await context.Set<Article>()
                .Where(a => a.FeedId == feed.Id)
                .Select(a => a.Link)
                .ToHashSetAsync(cancellationToken);

            int newArticlesCount = 0;

            foreach (var item in syndicationFeed.Items)
            {
                var articleLink = item.Links.FirstOrDefault()?.Uri.ToString();

                if (string.IsNullOrEmpty(articleLink) || existingArticleLinks.Contains(articleLink))
                {
                    continue;
                }

                var article = CreateArticleFromSyndicationItem(item, feed);
                context.Set<Article>().Add(article);
                newArticlesCount++;
            }

            await context.SaveChangesAsync(cancellationToken);

            _logger?.LogInformation(
                "Updated feed {FeedId} ({FeedTitle}): {NewArticles} new articles",
                feed.Id, feed.Title, newArticlesCount);
        }

        private Article CreateArticleFromSyndicationItem(SyndicationItem item, Feed feed)
        {
            return new Article
            {
                Title = item.Title.Text,
                Author = item.Authors.FirstOrDefault()?.Name,
                Content = GetArticleContent(item),
                Link = item.Links.FirstOrDefault()?.Uri.ToString(),
                PublishDate = item.PublishDate.DateTime != default
                    ? item.PublishDate.DateTime
                    : item.LastUpdatedTime.DateTime,
                IsRead = false,
                IsFavorite = false,
                Feed = feed,
                FeedId = feed.Id
            };
        }

        private string? GetArticleContent(SyndicationItem item)
        {
            if (item.Content is TextSyndicationContent textContent)
            {
                return textContent.Text;
            }

            var contentEncoded = item.ElementExtensions
                .FirstOrDefault(e => e.OuterName == "encoded" &&
                                   e.OuterNamespace == "http://purl.org/rss/1.0/modules/content/");

            if (contentEncoded != null)
            {
                return contentEncoded.GetObject<XmlElement>().InnerText;
            }

            return item.Summary?.Text;
        }

        private async Task<string?> GetImageUrlAsync(SyndicationFeed syndicationFeed)
        {
            if (syndicationFeed.ImageUrl != null)
            {
                return syndicationFeed.ImageUrl.ToString();
            }

            var links = syndicationFeed.Links
                .Where(l => l.Uri != null)
                .Select(l => l.Uri.ToString())
                .Distinct()
                .ToList();

            if (!links.Any())
            {
                return null;
            }

            foreach (var link in links)
            {
                try
                {
                    var iconUrl = await ParseIconFromHtmlAsync(link);
                    if (!string.IsNullOrEmpty(iconUrl))
                    {
                        return iconUrl;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Error parsing icon from HTML for {Link}", link);
                }
            }

            foreach (var link in links)
            {
                try
                {
                    var uri = new Uri(link);
                    var faviconUrl = $"{uri.Scheme}://{uri.Host}/favicon.ico";

                    if (await CheckUrlExistsAsync(faviconUrl))
                    {
                        return faviconUrl;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Error checking favicon for {Link}", link);
                }
            }

            try
            {
                var firstUri = new Uri(links.First());
                return $"{firstUri.Scheme}://{firstUri.Host}/favicon.ico";
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error getting default favicon URL");
                return null;
            }
        }

        private async Task<bool> CheckUrlExistsAsync(string url)
        {
            if (_httpClientFactory == null)
            {
                return false;
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            try
            {
                var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Failed to check URL {Url}", url);
                return false;
            }
        }

        private async Task<string?> ParseIconFromHtmlAsync(string url)
        {
            if (_httpClientFactory == null)
            {
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            try
            {
                var html = await client.GetStringAsync(url);
                var baseUri = new Uri(url);

                var iconPatterns = new[]
                {
                    @"<link[^>]*rel=['""](?:icon|shortcut icon)['""][^>]*href=['""]([^'""]+)['""]",
                    @"<link[^>]*href=['""]([^'""]+)['""][^>]*rel=['""](?:icon|shortcut icon)['""]",
                    @"<link[^>]*rel=['""]apple-touch-icon['""][^>]*href=['""]([^'""]+)['""]",
                    @"<link[^>]*href=['""]([^'""]+)['""][^>]*rel=['""]apple-touch-icon['""]",
                    @"<meta[^>]*property=['""]og:image['""][^>]*content=['""]([^'""]+)['""]",
                    @"<meta[^>]*content=['""]([^'""]+)['""][^>]*property=['""]og:image['""]"
                };

                foreach (var pattern in iconPatterns)
                {
                    var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var iconUrl = match.Groups[1].Value;
                        return NormalizeIconUrl(iconUrl, baseUri);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error fetching HTML from {Url}", url);
            }

            return null;
        }

        private string NormalizeIconUrl(string iconUrl, Uri baseUri)
        {
            if (iconUrl.StartsWith("//"))
            {
                return $"{baseUri.Scheme}:{iconUrl}";
            }
            else if (iconUrl.StartsWith("/"))
            {
                return $"{baseUri.Scheme}://{baseUri.Host}{iconUrl}";
            }
            else if (!iconUrl.StartsWith("http"))
            {
                return new Uri(baseUri, iconUrl).ToString();
            }

            return iconUrl;
        }

        public class FeedUpdateResult
        {
            public int TotalFeeds { get; set; }
            public int SuccessfulUpdates { get; set; }
            public int FailedUpdates { get; set; }
            public List<string> Errors { get; set; } = new();
        }
    }
}
