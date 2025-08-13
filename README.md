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
- **Pre/Post Navigation Hooks**: Extensible middleware for request/response processing
- **URL Filtering**: Regex-based URL blocking and filtering
- **File Storage**: Persistent file-based datasets and key-value stores
- **Proxy Support**: Advanced proxy configuration and rotation
- **Request State Tracking**: Detailed request lifecycle management
- **Enhanced Error Handling**: Comprehensive error tracking and reporting

## Installation

```bash
dotnet add package Microsoft.Playwright
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
dotnet add package Newtonsoft.Json
dotnet add package System.Text.Json
dotnet add package Polly
dotnet add package Serilog
dotnet add package System.ComponentModel.DataAnnotations
```

## Quick Start

```csharp
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    MaxConcurrency = 5,
    RequestDelayMilliseconds = 1000,
    ParseHtml = true,
    ParseJson = true,
    MaxRequestsPerCrawl = 100,
    IgnoreSslErrors = true,
    StreamResponse = true,
    BlockedUrls = new List<Regex>
    {
        new Regex(@".*\.(css|js|png|jpg)$", RegexOptions.IgnoreCase)
    }
});

// Add pre-navigation hook
crawler.AddPreNavigationHook(async (context) =>
{
    context.Request.Headers["X-Custom-Header"] = "MyValue";
    await Task.CompletedTask;
});

// Add post-navigation hook
crawler.AddPostNavigationHook(async (context) =>
{
    context.Response.Metadata["ProcessedAt"] = DateTime.UtcNow;
    await Task.CompletedTask;
});

// Handle failed requests
crawler.FailedRequestHandler(async (context, exception) =>
{
    Console.WriteLine($"Failed: {context.Request.Url} - {exception.Message}");
    await context.Dataset.PushData(new { 
        Url = context.Request.Url, 
        Error = exception.Message,
        RequestState = context.Request.State.ToString(),
        RetryCount = context.Request.RetryCount
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
        ResponseTime = context.Response.ResponseTime.TotalMilliseconds,
        ContentType = context.Response.ContentType,
        Encoding = context.Response.Encoding,
        RequestId = context.Request.Id
    });
    
    // Enqueue more links
    var links = context.Response.Html?.QuerySelectorAll("a[href]")
        .Select(a => a.GetAttribute("href"))
        .Where(href => !string.IsNullOrEmpty(href))
        .Where(href => Uri.TryCreate(new Uri(context.Request.Url), href, out _));
        
    if (links?.Any() == true)
    {
        await context.EnqueueLinks(links, 
            userData: new Dictionary<string, object> { ["depth"] = 1 },
            label: "DETAIL");
    }
});

// Add requests with metadata
var requests = new[]
{
    new Request("https://example.com", new Dictionary<string, object> { ["depth"] = 0 })
};
await crawler.AddRequests(requests);

// Get statistics
var stats = crawler.GetStatistics().GetSnapshot();
Console.WriteLine($"Processed {stats.RequestsFinished} requests");
Console.WriteLine($"Success rate: {stats.RequestsFinished / (double)(stats.RequestsFinished + stats.RequestsFailed):P2}");
Console.WriteLine($"Average response time: {stats.RequestAvgDurationMillis:F2}ms");
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
- `ProxyConfiguration` - Advanced proxy management
- `FileDataset` - File-based data persistence
- `FileKeyValueStore` - File-based key-value storage

## Advanced Features

### Pre/Post Navigation Hooks

```csharp
crawler.AddPreNavigationHook(async (context) =>
{
    // Modify request before sending
    if (context.Request.Url.Contains("api"))
    {
        context.Request.Headers["Accept"] = "application/json";
    }
});

crawler.AddPostNavigationHook(async (context) =>
{
    // Process response after receiving
    context.Response.Metadata["ProcessedAt"] = DateTime.UtcNow;
});
```

### URL Filtering

```csharp
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    BlockedUrls = new List<Regex>
    {
        new Regex(@".*\.(css|js|png|jpg|jpeg|gif|ico|svg)$", RegexOptions.IgnoreCase),
        new Regex(@".*\/ads\/.*", RegexOptions.IgnoreCase)
    }
});
```

### File Storage

```csharp
// Use file-based storage instead of memory
var dataset = new FileDataset("./data/my-dataset");
var keyValueStore = new FileKeyValueStore("./data/my-store");
```

### Proxy Configuration

```csharp
var proxyConfig = new ProxyConfiguration(new[]
{
    "http://proxy1:8080",
    "http://user:pass@proxy2:8080"
});

var proxy = proxyConfig.GetNextProxy();
```

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
        var stream = await context.Response.GetStreamAsync();
        await context.KeyValueStore.SetValue($"pdf_{context.Request.Id}", stream);
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

### Request State Tracking

```csharp
// Monitor request states
Console.WriteLine($"Request state: {request.State}"); // Unprocessed, InProgress, Handled, Failed
Console.WriteLine($"Retry count: {request.RetryCount}");
Console.WriteLine($"Error messages: {string.Join(", ", request.ErrorMessages)}");
```

## License

MIT License