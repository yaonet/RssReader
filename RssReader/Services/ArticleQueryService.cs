using Microsoft.EntityFrameworkCore;
using RssReader.Data;
using RssReader.Models;
using RssReader.ViewModels;

namespace RssReader.Services;

public class ArticleQueryService
{
    private readonly IDbContextFactory<RssReaderContext> _dbFactory;
    private readonly ILogger<ArticleQueryService> _logger;

    public ArticleQueryService(
        IDbContextFactory<RssReaderContext> dbFactory,
        ILogger<ArticleQueryService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<ArticleQueryResult> GetArticlesAsync(
        int? feedId = null,
        int? categoryId = null,
        bool? isRead = null,
        bool? isFavorite = null,
        string? searchTerm = null,
        int skip = 0,
        int take = 100)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var query = context.Set<Article>()
                .AsNoTracking();

            query = ApplyFilters(query, feedId, categoryId, isRead, isFavorite, searchTerm);

            var articles = await query
                .OrderBy(a => a.IsRead)
                .ThenByDescending(a => a.PublishDate)
                .Skip(skip)
                .Take(take)
                .Select(a => new ArticleListItem
                {
                    Id = a.Id,
                    Title = a.Title,
                    Link = a.Link,
                    PublishDate = a.PublishDate,
                    IsRead = a.IsRead,
                    IsFavorite = a.IsFavorite,
                    FeedId = a.FeedId,
                    FeedTitle = a.Feed != null ? a.Feed.Title : null,
                    FeedLink = a.Feed != null ? a.Feed.Link : null,
                    FeedImageUrl = a.Feed != null ? a.Feed.ImageUrl : null,
                    CategoryName = a.Feed != null && a.Feed.Category != null ? a.Feed.Category.Name : null
                })
                .ToListAsync();

            return new ArticleQueryResult
            {
                Articles = articles,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying articles. FeedId: {FeedId}, CategoryId: {CategoryId}, Skip: {Skip}, Take: {Take}",
                feedId, categoryId, skip, take);
            return new ArticleQueryResult
            {
                Articles = new List<ArticleListItem>(),
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<FeedFilterInfo?> GetFeedFilterInfoAsync(int feedId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            return await context.Feed
                .AsNoTracking()
                .Where(f => f.Id == feedId)
                .Select(f => new FeedFilterInfo
                {
                    FeedTitle = f.Title,
                    CategoryName = f.Category != null ? f.Category.Name : null
                })
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting feed filter info for FeedId: {FeedId}", feedId);
            return null;
        }
    }

    public async Task<string?> GetCategoryNameAsync(int categoryId)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            return await context.Category
                .AsNoTracking()
                .Where(c => c.Id == categoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category name for CategoryId: {CategoryId}", categoryId);
            return null;
        }
    }

    public async Task<int> MarkArticlesAsReadAsync(
            int? feedId = null,
            int? categoryId = null,
            bool? isRead = null,
            bool? isFavorite = null,
            string? searchTerm = null)
    {
        try
        {
            await using var context = await _dbFactory.CreateDbContextAsync();

            var query = context.Set<Article>().AsQueryable();

            query = ApplyFilters(query, feedId, categoryId, isRead, isFavorite, searchTerm);

            return await query
                .Where(a => !a.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsRead, true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking articles as read");
            return 0;
        }
    }

    private IQueryable<Article> ApplyFilters(
        IQueryable<Article> query,
        int? feedId,
        int? categoryId,
        bool? isRead,
        bool? isFavorite,
        string? searchTerm)
    {
        if (feedId.HasValue && feedId.Value > 0)
        {
            query = query.Where(a => a.FeedId == feedId.Value);
        }
        else if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(a => a.Feed!.CategoryId == categoryId.Value);
        }

        if (isRead.HasValue)
        {
            query = query.Where(a => a.IsRead == isRead.Value);
        }

        if (isFavorite.HasValue)
        {
            query = query.Where(a => a.IsFavorite == isFavorite.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var keywords = searchTerm.Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .ToList();

            if (keywords.Any())
            {
                foreach (var keyword in keywords)
                {
                    var k = keyword;
#pragma warning disable CS8602 // Dereference of a possibly null reference - safe in EF query context
                    query = query.Where(a =>
                        (a.Title != null && a.Title.Contains(k)) ||
                        (a.Feed != null && a.Feed.Title != null && a.Feed.Title.Contains(k)) ||
                        (a.Content != null && a.Content.Contains(k)) ||
                        (a.Author != null && a.Author.Contains(k)));
#pragma warning restore CS8602
                }
            }
        }

        return query;
    }
}

public class ArticleQueryResult
{
    public List<ArticleListItem> Articles { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class FeedFilterInfo
{
    public string? FeedTitle { get; set; }
    public string? CategoryName { get; set; }
}
