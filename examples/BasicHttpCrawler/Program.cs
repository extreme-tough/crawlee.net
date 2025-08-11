using Crawlee.NET.Crawlers;
using Crawlee.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<HttpCrawler>();

// Create crawler with options
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    MaxConcurrency = 5,
    RequestDelayMilliseconds = 1000,
    MaxRetries = 3,
    UserAgent = "Crawlee.NET Example Bot/1.0",
    ParseHtml = true,
    ParseJson = true,
    MaxRequestsPerCrawl = 50
}, logger);

// Set up failed request handler
crawler.FailedRequestHandler(async (context, exception) =>
{
    Console.WriteLine($"Failed to process: {context.Request.Url}");
    Console.WriteLine($"Error: {exception.Message}");
    
    // Log failed request data
    await context.Dataset.PushData(new {
        Url = context.Request.Url,
        Error = exception.Message,
        RetryCount = context.Request.RetryCount,
        FailedAt = DateTime.UtcNow
    });
});

// Set up request handler
await crawler.Run(async (context) =>
{
    var title = context.Response.Html?.QuerySelector("title")?.TextContent ?? "No title";
    var url = context.Request.Url;
    var statusCode = context.Response.StatusCode;
    var responseTime = context.Response.ResponseTime.TotalMilliseconds;
    
    Console.WriteLine($"Processing: {url}");
    Console.WriteLine($"Title: {title}");
    Console.WriteLine($"Status: {statusCode} ({responseTime}ms)");
    
    // Save data
    await context.Dataset.PushData(new { 
        Title = title, 
        Url = url, 
        StatusCode = statusCode,
        ResponseTimeMs = responseTime,
        ProcessedAt = DateTime.UtcNow,
        ContentLength = context.Response.ContentLength,
        ContentType = context.Response.ContentType
    });
    
    // Extract links and add them to queue (if needed)
    if (context.Response.Html != null)
    {
        var links = context.Response.Html
            .QuerySelectorAll("a[href]")
            .Take(5) // Limit to 5 links per page
            .Select(a => a.GetAttribute("href"))
            .Where(href => !string.IsNullOrEmpty(href) && Uri.TryCreate(context.Request.Url, href, out _))
            .Select(href => new Uri(new Uri(context.Request.Url), href).ToString());
            
        await context.EnqueueLinks(links, label: "DETAIL");
        
        context.Log("Found {LinkCount} links to enqueue", links.Count());
    }
});

// Add initial requests
await crawler.AddRequests(
    "https://example.com",
    "https://httpbin.org/html",
    "https://quotes.toscrape.com",
    "https://httpbin.org/json"
);

Console.WriteLine("Crawler finished successfully!");

// Get and display statistics
var stats = crawler.GetStatistics().GetSnapshot();
Console.WriteLine($"\n=== Crawling Statistics ===");
Console.WriteLine($"Requests finished: {stats.RequestsFinished}");
Console.WriteLine($"Requests failed: {stats.RequestsFailed}");
Console.WriteLine($"Requests retried: {stats.RequestsRetries}");
Console.WriteLine($"Average response time: {stats.RequestAvgDurationMillis:F2}ms");
Console.WriteLine($"Min response time: {stats.RequestMinDurationMillis}ms");
Console.WriteLine($"Max response time: {stats.RequestMaxDurationMillis}ms");
Console.WriteLine($"Requests per minute: {stats.RequestsPerMinute:F2}");
Console.WriteLine($"Total runtime: {stats.CrawlerRuntimeMillis}");

// Get final dataset
var data = await crawler._dataset.GetData<dynamic>();
Console.WriteLine($"Scraped {await crawler._dataset.GetItemCount()} items total");