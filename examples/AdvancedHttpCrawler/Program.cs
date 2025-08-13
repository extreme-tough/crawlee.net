using Crawlee.NET.Crawlers;
using Crawlee.NET.Models;
using Crawlee.NET.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<HttpCrawler>();

// Create advanced crawler with comprehensive options
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    MaxConcurrency = 10,
    RequestDelayMilliseconds = 500,
    MaxRetries = 5,
    UserAgent = "Crawlee.NET Advanced Bot/2.0",
    ParseHtml = true,
    ParseJson = true,
    MaxRequestsPerCrawl = 100,
    IgnoreSslErrors = true,
    StreamResponse = true,
    MaxResponseSize = 10 * 1024 * 1024, // 10MB
    BlockedUrls = new List<Regex>
    {
        new Regex(@".*\.(css|js|png|jpg|jpeg|gif|ico|svg)$", RegexOptions.IgnoreCase),
        new Regex(@".*\/ads\/.*", RegexOptions.IgnoreCase)
    }
}, logger);

// Add pre-navigation hook for request modification
crawler.AddPreNavigationHook(async (context) =>
{
    // Add custom headers based on URL
    if (context.Request.Url.Contains("api"))
    {
        context.Request.Headers["Accept"] = "application/json";
        context.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
    }
    
    logger.LogDebug("Pre-navigation hook executed for: {Url}", context.Request.Url);
    await Task.CompletedTask;
});

// Add post-navigation hook for response processing
crawler.AddPostNavigationHook(async (context) =>
{
    // Log response metadata
    context.Response.Metadata["ProcessedAt"] = DateTime.UtcNow;
    context.Response.Metadata["ResponseSize"] = context.Response.Body.Length;
    
    logger.LogDebug("Post-navigation hook executed for: {Url}, Status: {Status}", 
        context.Request.Url, context.Response.StatusCode);
    await Task.CompletedTask;
});

// Enhanced failed request handler with detailed logging
crawler.FailedRequestHandler(async (context, exception) =>
{
    logger.LogError(exception, "Failed to process: {Url}", context.Request.Url);
    
    await context.Dataset.PushData(new {
        Url = context.Request.Url,
        Error = exception.Message,
        RetryCount = context.Request.RetryCount,
        FailedAt = DateTime.UtcNow,
        RequestState = context.Request.State.ToString(),
        ErrorMessages = context.Request.ErrorMessages
    });
});

// Main request handler with advanced features
await crawler.Run(async (context) =>
{
    var title = context.Response.Html?.QuerySelector("title")?.TextContent ?? "No title";
    var url = context.Request.Url;
    var statusCode = context.Response.StatusCode;
    var responseTime = context.Response.ResponseTime.TotalMilliseconds;
    
    logger.LogInformation("Processing: {Url} [{Status}] ({ResponseTime}ms)", url, statusCode, responseTime);
    
    // Extract structured data
    var structuredData = new
    {
        Title = title,
        Url = url,
        StatusCode = statusCode,
        ResponseTimeMs = responseTime,
        ProcessedAt = DateTime.UtcNow,
        ContentLength = context.Response.ContentLength,
        ContentType = context.Response.ContentType,
        Encoding = context.Response.Encoding,
        Metadata = context.Response.Metadata,
        RequestId = context.Request.Id,
        RequestLabel = context.Request.Label,
        RetryCount = context.Request.RetryCount
    };
    
    // Save to dataset
    await context.Dataset.PushData(structuredData);
    
    // Extract and process links with advanced filtering
    if (context.Response.Html != null)
    {
        var links = context.Response.Html
            .QuerySelectorAll("a[href]")
            .Take(10) // Limit links per page
            .Select(a => a.GetAttribute("href"))
            .Where(href => !string.IsNullOrEmpty(href))
            .Where(href => Uri.TryCreate(new Uri(context.Request.Url), href, out var absoluteUri) && 
                          (absoluteUri.Scheme == "http" || absoluteUri.Scheme == "https"))
            .Select(href => new Uri(new Uri(context.Request.Url), href).ToString())
            .Where(absoluteUrl => !absoluteUrl.Contains("#")) // Skip anchor links
            .Distinct();
            
        if (links.Any())
        {
            await context.EnqueueLinks(links, 
                userData: new Dictionary<string, object> 
                { 
                    ["parentUrl"] = context.Request.Url,
                    ["depth"] = (context.Request.UserData.GetValueOrDefault("depth", 0) as int? ?? 0) + 1
                }, 
                label: "DETAIL");
            
            context.Log("Enqueued {LinkCount} links from {Url}", links.Count(), context.Request.Url);
        }
        
        // Extract meta information
        var metaDescription = context.Response.Html.QuerySelector("meta[name='description']")?.GetAttribute("content");
        var metaKeywords = context.Response.Html.QuerySelector("meta[name='keywords']")?.GetAttribute("content");
        
        if (!string.IsNullOrEmpty(metaDescription) || !string.IsNullOrEmpty(metaKeywords))
        {
            await context.KeyValueStore.SetValue($"meta_{context.Request.Id}", new
            {
                Url = context.Request.Url,
                Description = metaDescription,
                Keywords = metaKeywords,
                ExtractedAt = DateTime.UtcNow
            });
        }
    }
    
    // Handle JSON responses
    if (context.Response.Json != null)
    {
        var jsonData = context.Response.JsonAs<dynamic>();
        await context.KeyValueStore.SetValue($"json_{context.Request.Id}", jsonData);
        context.Log("Processed JSON response from {Url}", context.Request.Url);
    }
});

// Add initial requests with metadata
var initialRequests = new[]
{
    new Request("https://example.com", new Dictionary<string, object> { ["depth"] = 0 }),
    new Request("https://httpbin.org/html", new Dictionary<string, object> { ["depth"] = 0 }),
    new Request("https://quotes.toscrape.com", new Dictionary<string, object> { ["depth"] = 0 }),
    new Request("https://httpbin.org/json", new Dictionary<string, object> { ["depth"] = 0 })
};

await crawler.AddRequests(initialRequests);

logger.LogInformation("Advanced crawler finished successfully!");

// Display comprehensive statistics
var stats = crawler.GetStatistics().GetSnapshot();
logger.LogInformation("\n=== Advanced Crawling Statistics ===");
logger.LogInformation("Requests finished: {RequestsFinished}", stats.RequestsFinished);
logger.LogInformation("Requests failed: {RequestsFailed}", stats.RequestsFailed);
logger.LogInformation("Requests retried: {RequestsRetries}", stats.RequestsRetries);
logger.LogInformation("Success rate: {SuccessRate:P2}", 
    stats.RequestsFinished / (double)(stats.RequestsFinished + stats.RequestsFailed));
logger.LogInformation("Average response time: {AvgResponseTime:F2}ms", stats.RequestAvgDurationMillis);
logger.LogInformation("Min response time: {MinResponseTime}ms", stats.RequestMinDurationMillis);
logger.LogInformation("Max response time: {MaxResponseTime}ms", stats.RequestMaxDurationMillis);
logger.LogInformation("Requests per minute: {RequestsPerMinute:F2}", stats.RequestsPerMinute);
logger.LogInformation("Total runtime: {TotalRuntime}", stats.CrawlerRuntimeMillis);

// Display final counts
var totalItems = await crawler._dataset.GetItemCount();
logger.LogInformation("Total items scraped: {TotalItems}", totalItems);

logger.LogInformation("Crawling completed successfully!");