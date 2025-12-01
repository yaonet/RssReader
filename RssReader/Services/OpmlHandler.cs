using System.Xml;
using System.Xml.Linq;
using System.Text;
using RssReader.Models;
using Microsoft.EntityFrameworkCore;
using RssReader.Data;

namespace RssReader.Services
{
    public class OpmlHandler
    {
        private readonly IDbContextFactory<RssReaderContext> _dbFactory;

        public OpmlHandler(IDbContextFactory<RssReaderContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public class OpmlFeed
        {
            public string? Title { get; set; }
            public string? XmlUrl { get; set; }
            public string? HtmlUrl { get; set; }
            public string? Category { get; set; }
        }

        public class OpmlImportResult
        {
            public List<OpmlFeed> Feeds { get; set; } = new();
            public bool Success { get; set; }
            public string? ErrorMessage { get; set; }
        }

        public async Task<OpmlImportResult> ParseOpmlFileAsync(Stream opmlStream)
        {
            var result = new OpmlImportResult();

            try
            {
                using var memoryStream = new MemoryStream();
                await opmlStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    Async = true,
                    IgnoreWhitespace = true,
                    CheckCharacters = false
                };

                using var reader = XmlReader.Create(memoryStream, settings);
                var document = await XDocument.LoadAsync(reader, LoadOptions.None, CancellationToken.None);
                var outlines = document.Descendants("outline");

                foreach (var outline in outlines)
                {
                    var xmlUrl = outline.Attribute("xmlUrl")?.Value;
                    
                    if (!string.IsNullOrEmpty(xmlUrl))
                    {
                        var feed = new OpmlFeed
                        {
                            Title = outline.Attribute("title")?.Value ?? outline.Attribute("text")?.Value,
                            XmlUrl = xmlUrl,
                            HtmlUrl = outline.Attribute("htmlUrl")?.Value,
                            Category = GetCategoryFromOutline(outline)
                        };
                        
                        result.Feeds.Add(feed);
                    }
                }

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Failed to parse OPML file: {ex.Message}";
            }

            return result;
        }

        public async Task<string> ExportOpmlAsync()
        {
            await using var context = await _dbFactory.CreateDbContextAsync();
            
            var feeds = await context.Feed
                .Include(f => f.Category)
                .OrderBy(f => f.Category!.Name)
                .ThenBy(f => f.Title)
                .AsNoTracking()
                .ToListAsync();

            var feedsByCategory = feeds.GroupBy(f => f.Category?.Name ?? "Uncategorized");

            var opml = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement("opml",
                    new XAttribute("version", "2.0"),
                    new XElement("head",
                        new XElement("title", "RSS Reader Feeds"),
                        new XElement("dateCreated", DateTime.UtcNow.ToString("R"))
                    ),
                    new XElement("body",
                        feedsByCategory.Select(category =>
                            new XElement("outline",
                                new XAttribute("text", category.Key),
                                new XAttribute("title", category.Key),
                                category.Select(feed =>
                                    new XElement("outline",
                                        new XAttribute("type", "rss"),
                                        new XAttribute("text", feed.Title ?? "Untitled"),
                                        new XAttribute("title", feed.Title ?? "Untitled"),
                                        new XAttribute("xmlUrl", feed.Url ?? ""),
                                        feed.Link != null ? new XAttribute("htmlUrl", feed.Link) : null
                                    )
                                )
                            )
                        )
                    )
                )
            );

            var settings = new XmlWriterSettings
            {
                Indent = true,
                Async = true,
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false
            };

            using var stringWriter = new Utf8StringWriter();
            await using var xmlWriter = XmlWriter.Create(stringWriter, settings);
            await opml.SaveAsync(xmlWriter, CancellationToken.None);
            await xmlWriter.FlushAsync();

            return stringWriter.ToString();
        }

        private string? GetCategoryFromOutline(XElement outline)
        {
            var parent = outline.Parent;
            while (parent != null && parent.Name.LocalName == "outline")
            {
                var categoryTitle = parent.Attribute("title")?.Value ?? parent.Attribute("text")?.Value;
                if (!string.IsNullOrEmpty(categoryTitle))
                {
                    return categoryTitle;
                }
                parent = parent.Parent;
            }
            return null;
        }

        private class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => new UTF8Encoding(false);
        }
    }
}
