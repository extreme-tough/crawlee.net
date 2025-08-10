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

## Installation

```bash
dotnet add package Microsoft.Playwright
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Extensions.Logging
dotnet add package Newtonsoft.Json
```

## Quick Start

```csharp
var crawler = new HttpCrawler(new HttpCrawlerOptions
{
    MaxConcurrency = 5,
    RequestDelayMilliseconds = 1000
});

await crawler.Run(async (context) =>
{
    var title = context.Response.Html.QuerySelector("title")?.TextContent;
    await context.Dataset.PushData(new { Title = title, Url = context.Request.Url });
});

await crawler.AddRequests("https://example.com");
```

## Project Structure

This library provides the following main components:

- `HttpCrawler` - Fast HTTP-based crawling
- `BrowserCrawler` - Browser automation with Playwright
- `RequestQueue` - Request management and deduplication  
- `Dataset` - Structured data storage
- `KeyValueStore` - Key-value data persistence
- `SessionPool` - Session and proxy management

## License

MIT License