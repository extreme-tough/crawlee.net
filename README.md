# Crawlee.NET - Full-featured scraping toolkit for .NET Core

A comprehensive web scraping and crawling library for .NET Core, inspired by the popular Crawlee library for Node.js.

## Features

- **HTTP Crawling**: Fast HTTP-based crawling with automatic retries and rate limiting
- **Browser Automation**: Puppeteer/Playwright integration for JavaScript-heavy sites
- **Request Queue Management**: Priority queues with deduplication
- **Data Storage**: Datasets and key-value stores for structured data
- **Session Management**: Automatic session rotation and proxy support
- **Rate Limiting**: Configurable delays and concurrent request limits
- **Middleware Pipeline**: Extensible request/response processing
- **Autoscaling**: Dynamic concurrency adjustment based on system load
- **Statistics**: Comprehensive crawling metrics and performance monitoring
- **Retry Policies**: Exponential backoff with Polly integration
- **JSON Support**: Automatic JSON parsing and typed responses
- **Failed Request Handling**: Custom error handling and recovery

## Installation

```bash
dotnet add package Microsoft.Playwright
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
dotnet add package Newtonsoft.Json
dotnet add package System.Text.Json
dotnet add package Polly
dotnet add package Serilog
```

## Quick Start

```csharp
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    MaxConcurrency = 5,
    RequestDelayMilliseconds = 1000,
    ParseHtml = true,
    ParseJson = true,
    MaxRequestsPerCrawl = 100
});

// Handle failed requests
crawler.FailedRequestHandler(async (context, exception) =>
{
    Console.WriteLine($"Failed: {context.Request.Url} - {exception.Message}");
    await context.Dataset.PushData(new { 
        Url = context.Request.Url, 
        Error = exception.Message 
    });
});

await crawler.Run(async (context) =>
{
    var title = context.Response.Html.QuerySelector("title")?.TextContent;
    
    // Log with built-in logger
    context.Log("Processing: {Url}", context.Request.Url);
    
    await context.Dataset.PushData(new { 
        Title = title, 
        Url = context.Request.Url,
        ResponseTime = context.Response.ResponseTime.TotalMilliseconds
    });
    
    // Enqueue more links
    var links = context.Response.Html?.QuerySelectorAll("a[href]")
        .Select(a => a.GetAttribute("href"))
        .Where(href => !string.IsNullOrEmpty(href));
        
    if (links?.Any() == true)
    {
        await context.EnqueueLinks(links, label: "DETAIL");
    }
});

await crawler.AddRequests("https://example.com");

// Get statistics
var stats = crawler.GetStatistics().GetSnapshot();
Console.WriteLine($"Processed {stats.RequestsFinished} requests in {stats.CrawlerRuntimeMillis}");
```

## Project Structure

This library provides the following main components:

- `HttpCrawler` - Fast HTTP-based crawling
- `BrowserCrawler` - Browser automation with Playwright
- `RequestQueue` - Request management and deduplication  
- `Dataset` - Structured data storage
- `KeyValueStore` - Key-value data persistence
- `SessionPool` - Session and proxy management
- `AutoscaledPool` - Dynamic concurrency management
- `Statistics` - Performance metrics and monitoring

## Advanced Features

### Session Management

```csharp
var sessionPool = new SessionPool(new SessionPoolOptions
{
    MaxPoolSize = 100,
    SessionTtlSeconds = 1800
});

var session = await sessionPool.GetSession();
// Use session for requests
await sessionPool.ReturnSession(session);
```

### Custom Request Handling

```csharp
await crawler.Run(async (context) =>
{
    // Access JSON responses
    if (context.Response.Json != null)
    {
        var data = context.Response.JsonAs<MyDataType>();
        await context.Dataset.PushData(data);
    }
    
    // Handle different content types
    if (context.Response.ContentType?.Contains("application/pdf") == true)
    {
        await context.KeyValueStore.SetValue($"pdf_{context.Request.Id}", context.Response.Buffer);
    }
});
```

### Statistics and Monitoring

```csharp
var stats = crawler.GetStatistics().GetSnapshot();
Console.WriteLine($"Success Rate: {(double)stats.RequestsFinished / (stats.RequestsFinished + stats.RequestsFailed) * 100:F2}%");
Console.WriteLine($"Avg Response Time: {stats.RequestAvgDurationMillis:F2}ms");
Console.WriteLine($"Requests/min: {stats.RequestsPerMinute:F2}");
```
## License

MIT License