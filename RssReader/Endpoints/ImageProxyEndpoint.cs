using Microsoft.AspNetCore.Mvc;

namespace RssReader.Endpoints
{
    public static class ImageProxyEndpoint
    {
        public static void MapImageProxyEndpoint(this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/image-proxy", async (
                [FromQuery] string url,
                [FromServices] IHttpClientFactory httpClientFactory,
                [FromServices] ILogger<Program> logger,
                HttpContext context) =>
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return Results.BadRequest("URL is required");
                }

                try
                {
                    // Validate URL
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    {
                        return Results.BadRequest("Invalid URL");
                    }

                    // Only allow http and https schemes
                    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    {
                        return Results.BadRequest("Only HTTP and HTTPS URLs are allowed");
                    }

                    var client = httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                    var response = await client.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogWarning("Failed to fetch image from {Url}, status: {StatusCode}", url, response.StatusCode);
                        return Results.NotFound();
                    }

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/png";
                    
                    // Only allow image content types
                    if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogWarning("URL {Url} returned non-image content type: {ContentType}", url, contentType);
                        return Results.BadRequest("URL must point to an image");
                    }

                    var imageData = await response.Content.ReadAsByteArrayAsync();
                    
                    // Add cache headers
                    context.Response.Headers.CacheControl = "public, max-age=86400";
                    
                    return Results.File(imageData, contentType);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogWarning(ex, "HTTP error fetching image from {Url}", url);
                    return Results.NotFound();
                }
                catch (TaskCanceledException ex)
                {
                    logger.LogWarning(ex, "Timeout fetching image from {Url}", url);
                    return Results.NotFound();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error fetching image from {Url}", url);
                    return Results.Problem("Failed to fetch image");
                }
            })
            .WithName("ImageProxy");
        }
    }
}
