using Crawlee.NET.Crawlers;
using Crawlee.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<HttpCrawler>();

// Create crawler with options
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    MaxConcurrency = 5,
    RequestDelayMilliseconds = 1000,
    MaxRetries = 3,
    UserAgent = "Crawlee.NET Example Bot/1.0"
}, logger);

// Set up request handler
await crawler.Run(async (context) =>
{
    var title = context.Response.Html?.QuerySelector("title")?.TextContent ?? "No title";
    var url = context.Request.Url;
    
    Console.WriteLine($"Processing: {url}");
    Console.WriteLine($"Title: {title}");
    Console.WriteLine($"Status: {context.Response.StatusCode}");
    
    // Save data
    await context.Dataset.PushData(new { 
        Title = title, 
        Url = url, 
        StatusCode = context.Response.StatusCode,
        ProcessedAt = DateTime.UtcNow
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
            
        await context.EnqueueLinks(links);
    }
});

// Add initial requests
await crawler.AddRequests(
    "https://example.com",
    "https://httpbin.org/html",
    "https://quotes.toscrape.com"
);

Console.WriteLine("Crawler finished successfully!");

// Get final dataset
var data = await crawler._dataset.GetData<dynamic>();
Console.WriteLine($"Scraped {await crawler._dataset.GetItemCount()} items total");