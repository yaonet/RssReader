using System.Xml;
using System.Xml.Linq;
using RssReader.Models;

namespace RssReader.Services
{
    public class OpmlImportService
    {
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
                var settings = new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    Async = true
                };

                using var reader = XmlReader.Create(opmlStream, settings);
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
    }
}
