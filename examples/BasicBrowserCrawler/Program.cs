using Crawlee.NET.Crawlers;
using Crawlee.NET.Models;
using Microsoft.Extensions.Logging;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =&gt; builder.AddConsole());
var logger = loggerFactory.CreateLogger&lt;BrowserCrawler&gt;();

// Create browser crawler
var crawler = new BrowserCrawler(new BrowserCrawlerOptions
{
    MaxConcurrency = 3,
    RequestDelayMilliseconds = 2000,
    Headless = true,
    BrowserType = "chromium"
}, logger);

// Set up request handler
await crawler.Run(async (context) =&gt;
{
    Console.WriteLine($"Processing: {context.Request.Url}");
    
    // Wait for page to load
    await context.Page.WaitForLoadStateAsync();
    
    // Extract data using Playwright selectors
    var title = await context.Page.TitleAsync();
    var headings = await context.Page.Locator("h1, h2, h3").AllTextContentsAsync();
    
    Console.WriteLine($"Title: {title}");
    Console.WriteLine($"Found {headings.Count} headings");
    
    // Save scraped data
    await context.Dataset.PushData(new {
        Title = title,
        Url = context.Request.Url,
        Headings = headings,
        ProcessedAt = DateTime.UtcNow
    });
    
    // Take screenshot (optional)
    await context.Page.ScreenshotAsync(new()
    {
        Path = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
        FullPage = true
    });
});

// Add requests
await crawler.AddRequests(
    "https://example.com",
    "https://quotes.toscrape.com/js",
    "https://books.toscrape.com"
);

Console.WriteLine("Browser crawler finished!");
crawler.Dispose();