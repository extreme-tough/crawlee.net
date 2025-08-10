using Crawlee.NET.Models;
using Crawlee.NET.Queue;
using Crawlee.NET.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Crawlee.NET.Crawlers
{
    public class BrowserCrawlerOptions : CrawlerOptions
    {
        public string BrowserType { get; set; } = "chromium"; // chromium, firefox, webkit
        public bool Headless { get; set; } = true;
        public List&lt;string&gt; LaunchOptions { get; set; } = new();
        public int NavigationTimeoutSeconds { get; set; } = 30;
    }

    public class BrowserCrawler : IDisposable
    {
        private readonly BrowserCrawlerOptions _options;
        private readonly IRequestQueue _requestQueue;
        private readonly IDataset _dataset;
        private readonly IKeyValueStore _keyValueStore;
        private readonly ILogger&lt;BrowserCrawler&gt;? _logger;
        private readonly SemaphoreSlim _concurrencySemaphore;
        
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private Func&lt;BrowserCrawlingContext, Task&gt;? _requestHandler;
        private bool _isRunning;
        
        public BrowserCrawler(BrowserCrawlerOptions? options = null, ILogger&lt;BrowserCrawler&gt;? logger = null)
        {
            _options = options ?? new BrowserCrawlerOptions();
            _requestQueue = new MemoryRequestQueue();
            _dataset = new MemoryDataset();
            _keyValueStore = new MemoryKeyValueStore();
            _logger = logger;
            _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
        }
        
        public void Use(Func&lt;BrowserCrawlingContext, Task&gt; handler)
        {
            _requestHandler = handler;
        }
        
        public async Task AddRequests(params string[] urls)
        {
            var requests = urls.Select(url =&gt; new Request(url));
            await _requestQueue.AddRequests(requests);
        }
        
        public async Task AddRequests(IEnumerable&lt;Request&gt; requests)
        {
            await _requestQueue.AddRequests(requests);
        }
        
        public async Task Run(Func&lt;BrowserCrawlingContext, Task&gt;? handler = null)
        {
            if (handler != null)
                _requestHandler = handler;
                
            if (_requestHandler == null)
                throw new InvalidOperationException("Request handler must be set before running the crawler");
                
            await InitializeBrowser();
            
            _isRunning = true;
            var tasks = new List&lt;Task&gt;();
            
            _logger?.LogInformation("Starting browser crawler with {MaxConcurrency} max concurrency", _options.MaxConcurrency);
            
            while (_isRunning)
            {
                tasks.RemoveAll(t =&gt; t.IsCompleted);
                
                while (tasks.Count &lt; _options.MaxConcurrency && !await _requestQueue.IsEmpty())
                {
                    var request = await _requestQueue.FetchNextRequest();
                    if (request != null)
                    {
                        tasks.Add(ProcessRequest(request));
                    }
                    else
                    {
                        break;
                    }
                }
                
                if (tasks.Count == 0 && await _requestQueue.IsEmpty())
                {
                    break;
                }
                
                if (tasks.Count == 0)
                {
                    await Task.Delay(100);
                }
                else
                {
                    await Task.WhenAny(tasks);
                }
            }
            
            await Task.WhenAll(tasks);
            _logger?.LogInformation("Browser crawler finished");
        }
        
        private async Task InitializeBrowser()
        {
            _playwright = await Playwright.CreateAsync();
            
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = _options.Headless
            };
            
            _browser = _options.BrowserType.ToLower() switch
            {
                "firefox" =&gt; await _playwright.Firefox.LaunchAsync(launchOptions),
                "webkit" =&gt; await _playwright.Webkit.LaunchAsync(launchOptions),
                _ =&gt; await _playwright.Chromium.LaunchAsync(launchOptions)
            };
        }
        
        private async Task ProcessRequest(Request request)
        {
            await _concurrencySemaphore.WaitAsync();
            IPage? page = null;
            
            try
            {
                if (_options.RequestDelayMilliseconds &gt; 0)
                {
                    await Task.Delay(_options.RequestDelayMilliseconds);
                }
                
                page = await _browser!.NewPageAsync();
                await page.SetUserAgentAsync(_options.UserAgent);
                
                foreach (var header in _options.DefaultHeaders)
                {
                    await page.SetExtraHTTPHeadersAsync(new Dictionary&lt;string, string&gt; { { header.Key, header.Value } });
                }
                
                var response = await page.GotoAsync(request.Url, new PageGotoOptions
                {
                    Timeout = _options.NavigationTimeoutSeconds * 1000
                });
                
                var context = new BrowserCrawlingContext(request, page, response, _dataset, _keyValueStore);
                
                await _requestHandler!(context);
                await _requestQueue.MarkRequestHandled(request);
                
                _logger?.LogDebug("Successfully processed request: {Url}", request.Url);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing request: {Url}", request.Url);
                
                if (request.RetryCount &lt; request.MaxRetries)
                {
                    request.RetryCount++;
                    await Task.Delay(_options.RetryDelayMilliseconds);
                    await _requestQueue.ReclaimRequest(request);
                    _logger?.LogInformation("Retrying request: {Url} (attempt {RetryCount}/{MaxRetries})", 
                        request.Url, request.RetryCount, request.MaxRetries);
                }
                else
                {
                    await _requestQueue.MarkRequestHandled(request);
                    _logger?.LogWarning("Request failed after {MaxRetries} retries: {Url}", request.MaxRetries, request.Url);
                }
            }
            finally
            {
                if (page != null)
                {
                    await page.CloseAsync();
                }
                _concurrencySemaphore.Release();
            }
        }
        
        public void Stop()
        {
            _isRunning = false;
        }
        
        public async void Dispose()
        {
            if (_browser != null)
                await _browser.CloseAsync();
            _playwright?.Dispose();
            _concurrencySemaphore?.Dispose();
        }
    }
    
    public class BrowserCrawlingContext
    {
        public Request Request { get; set; }
        public IPage Page { get; set; }
        public IResponse? Response { get; set; }
        public IDataset Dataset { get; set; }
        public IKeyValueStore KeyValueStore { get; set; }
        public Session? Session { get; set; }
        public Dictionary&lt;string, object&gt; State { get; set; } = new();
        
        public BrowserCrawlingContext(Request request, IPage page, IResponse? response, IDataset dataset, IKeyValueStore keyValueStore)
        {
            Request = request;
            Page = page;
            Response = response;
            Dataset = dataset;
            KeyValueStore = keyValueStore;
        }
    }
}