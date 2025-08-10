using AngleSharp;
using AngleSharp.Html.Dom;
using Crawlee.NET.Models;
using Crawlee.NET.Queue;
using Crawlee.NET.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Crawlee.NET.Crawlers
{
    public class HttpCrawlerOptions : CrawlerOptions
    {
        public bool ParseHtml { get; set; } = true;
        public bool FollowRedirects { get; set; } = true;
    }

    public class HttpCrawler
    {
        private readonly HttpCrawlerOptions _options;
        private readonly IRequestQueue _requestQueue;
        private readonly IDataset _dataset;
        private readonly IKeyValueStore _keyValueStore;
        private readonly HttpClient _httpClient;
        private readonly ILogger&lt;HttpCrawler&gt;? _logger;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly IBrowsingContext? _browsingContext;
        
        private Func&lt;CrawlingContext, Task&gt;? _requestHandler;
        private bool _isRunning;
        
        public HttpCrawler(HttpCrawlerOptions? options = null, ILogger&lt;HttpCrawler&gt;? logger = null)
        {
            _options = options ?? new HttpCrawlerOptions();
            _requestQueue = new MemoryRequestQueue();
            _dataset = new MemoryDataset();
            _keyValueStore = new MemoryKeyValueStore();
            _logger = logger;
            _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
            
            foreach (var header in _options.DefaultHeaders)
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            if (_options.ParseHtml)
            {
                var config = Configuration.Default;
                _browsingContext = BrowsingContext.New(config);
            }
        }
        
        public void Use(Func&lt;CrawlingContext, Task&gt; handler)
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
        
        public async Task Run(Func&lt;CrawlingContext, Task&gt;? handler = null)
        {
            if (handler != null)
                _requestHandler = handler;
                
            if (_requestHandler == null)
                throw new InvalidOperationException("Request handler must be set before running the crawler");
                
            _isRunning = true;
            var tasks = new List&lt;Task&gt;();
            
            _logger?.LogInformation("Starting HTTP crawler with {MaxConcurrency} max concurrency", _options.MaxConcurrency);
            
            while (_isRunning)
            {
                // Remove completed tasks
                tasks.RemoveAll(t =&gt; t.IsCompleted);
                
                // Add new tasks if we have capacity
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
                
                // If no tasks are running and queue is empty, we're done
                if (tasks.Count == 0 && await _requestQueue.IsEmpty())
                {
                    break;
                }
                
                // Wait a bit before checking again
                if (tasks.Count == 0)
                {
                    await Task.Delay(100);
                }
                else
                {
                    await Task.WhenAny(tasks);
                }
            }
            
            // Wait for all remaining tasks to complete
            await Task.WhenAll(tasks);
            _logger?.LogInformation("HTTP crawler finished");
        }
        
        private async Task ProcessRequest(Request request)
        {
            await _concurrencySemaphore.WaitAsync();
            try
            {
                if (_options.RequestDelayMilliseconds &gt; 0)
                {
                    await Task.Delay(_options.RequestDelayMilliseconds);
                }
                
                var response = await MakeRequest(request);
                var context = new CrawlingContext(request, response, _dataset, _keyValueStore);
                
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
                _concurrencySemaphore.Release();
            }
        }
        
        private async Task&lt;Response&gt; MakeRequest(Request request)
        {
            using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
            
            foreach (var header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            if (!string.IsNullOrEmpty(request.Body))
            {
                httpRequest.Content = new StringContent(request.Body);
            }
            
            var httpResponse = await _httpClient.SendAsync(httpRequest);
            var body = await httpResponse.Content.ReadAsStringAsync();
            
            var response = new Response
            {
                StatusCode = httpResponse.StatusCode,
                Body = body,
                Url = request.Url,
                Request = request
            };
            
            foreach (var header in httpResponse.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }
            
            if (_options.ParseHtml && _browsingContext != null)
            {
                var contentType = httpResponse.Content.Headers.ContentType?.MediaType;
                if (contentType?.Contains("text/html") == true)
                {
                    response.Html = await _browsingContext.OpenAsync(req =&gt; req.Content(body).Address(request.Url)) as IHtmlDocument;
                }
            }
            
            return response;
        }
        
        public void Stop()
        {
            _isRunning = false;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            _concurrencySemaphore?.Dispose();
        }
    }
}