using AngleSharp;
using AngleSharp.Html.Dom;
using Crawlee.NET.Models;
using Crawlee.NET.Queue;
using Crawlee.NET.Storage;
using Crawlee.NET.Utils;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Crawlee.NET.Crawlers
{
    public class HttpCrawlerOptions : CrawlerOptions
    {
        public bool ParseHtml { get; set; } = true;
        public bool FollowRedirects { get; set; } = true;
        public bool ParseJson { get; set; } = true;
        public List<string> AdditionalMimeTypes { get; set; } = new();
        public bool PreNavigationHooks { get; set; } = true;
        public bool PostNavigationHooks { get; set; } = true;
        public Dictionary<string, string> SuggestResponseEncoding { get; set; } = new();
        public bool ForceResponseEncoding { get; set; } = false;
    }

    public class HttpCrawler
    {
        private readonly HttpCrawlerOptions _options;
        private readonly IRequestQueue _requestQueue;
        public readonly IDataset _dataset;
        private readonly IKeyValueStore _keyValueStore;
        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpCrawler>? _logger;
        private readonly AutoscaledPool _autoscaledPool;
        private readonly IBrowsingContext? _browsingContext;
        private readonly Statistics _statistics;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
        
        private Func<CrawlingContext, Task>? _requestHandler;
        private Func<CrawlingContext, Exception, Task>? _failedRequestHandler;
        private bool _isRunning;
        private int _processedRequests;
        private DateTime _startTime;
        
        public HttpCrawler(HttpCrawlerOptions? options = null, ILogger<HttpCrawler>? logger = null)
        {
            _options = options ?? new HttpCrawlerOptions();
            _requestQueue = new MemoryRequestQueue();
            _dataset = new MemoryDataset();
            _keyValueStore = new MemoryKeyValueStore();
            _logger = logger;
            _statistics = new Statistics();
            
            var poolOptions = new AutoscaledPoolOptions
            {
                MinConcurrency = _options.MinConcurrency,
                MaxConcurrency = _options.MaxConcurrency,
                DesiredConcurrency = Math.Min(_options.MaxConcurrency, 10)
            };
            _autoscaledPool = new AutoscaledPool(poolOptions, logger);
            
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _options.UserAgent);
            
            // Setup retry policy with exponential backoff
            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
                .Or<HttpRequestException>()
                .WaitAndRetryAsync(
                    _options.MaxRetries,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger?.LogWarning("Retry {RetryCount} for request after {Delay}ms", retryCount, timespan.TotalMilliseconds);
                        _statistics.IncrementCounter("requestsRetries");
                    });
            
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
        
        public void Use(Func<CrawlingContext, Task> handler)
        {
            _requestHandler = handler;
        }
        
        public void FailedRequestHandler(Func<CrawlingContext, Exception, Task> handler)
        {
            _failedRequestHandler = handler;
        }
        
        public async Task AddRequests(params string[] urls)
        {
            var requests = urls.Select(url => new Request(url));
            await _requestQueue.AddRequests(requests);
        }
        
        public async Task AddRequests(IEnumerable<Request> requests)
        {
            await _requestQueue.AddRequests(requests);
        }
        
        public async Task Run(Func<CrawlingContext, Task>? handler = null)
        {
            if (handler != null)
                _requestHandler = handler;
                
            if (_requestHandler == null)
                throw new InvalidOperationException("Request handler must be set before running the crawler");
                
            _isRunning = true;
            _startTime = DateTime.UtcNow;
            _autoscaledPool.Start();
            
            _logger?.LogInformation("Starting HTTP crawler with {MaxConcurrency} max concurrency", _options.MaxConcurrency);
            
            while (_isRunning)
            {
                if (_processedRequests >= _options.MaxRequestsPerCrawl)
                {
                    _logger?.LogInformation("Reached maximum requests limit: {MaxRequests}", _options.MaxRequestsPerCrawl);
                    break;
                }
                
                if (_options.MaxCrawlTime.HasValue && DateTime.UtcNow - _startTime > _options.MaxCrawlTime.Value)
                {
                    _logger?.LogInformation("Reached maximum crawl time: {MaxTime}", _options.MaxCrawlTime.Value);
                    break;
                }
                
                var request = await _requestQueue.FetchNextRequest();
                if (request != null)
                {
                    await _autoscaledPool.AddTask(() => ProcessRequest(request));
                }
                else
                {
                    if (await _requestQueue.IsEmpty() && _autoscaledPool.RunningTasks == 0)
                    {
                        break;
                    }
                    await Task.Delay(100);
                }
            }
            
            _autoscaledPool.Stop();
            
            var stats = _statistics.GetSnapshot();
            _logger?.LogInformation("HTTP crawler finished");
            _logger?.LogInformation("Statistics: {RequestsFinished} finished, {RequestsFailed} failed, {AvgDuration}ms avg duration", 
                stats.RequestsFinished, stats.RequestsFailed, stats.RequestAvgDurationMillis);
        }
        
        private async Task ProcessRequest(Request request)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                if (_options.RequestDelayMilliseconds > 0)
                {
                    await Task.Delay(_options.RequestDelayMilliseconds);
                }
                
                var response = await MakeRequest(request);
                var context = new CrawlingContext(request, response, _dataset, _keyValueStore, _requestQueue, _logger);
                
                await _requestHandler!(context);
                await _requestQueue.MarkRequestHandled(request);
                
                Interlocked.Increment(ref _processedRequests);
                _statistics.IncrementCounter("requestsFinished");
                _statistics.RecordRequestDuration(stopwatch.ElapsedMilliseconds);
                
                _logger?.LogDebug("Successfully processed request: {Url}", request.Url);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing request: {Url}", request.Url);
                request.PushErrorMessage(ex.Message);
                _statistics.IncrementCounter("requestsFailed");
                
                if (request.RetryCount < request.MaxRetries)
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
                    
                    if (_failedRequestHandler != null)
                    {
                        try
                        {
                            var response = new Response { Request = request, StatusCode = System.Net.HttpStatusCode.InternalServerError };
                            var context = new CrawlingContext(request, response, _dataset, _keyValueStore, _requestQueue, _logger);
                            await _failedRequestHandler(context, ex);
                        }
                        catch (Exception handlerEx)
                        {
                            _logger?.LogError(handlerEx, "Error in failed request handler for: {Url}", request.Url);
                        }
                    }
                }
            }
            finally
            {
                stopwatch.Stop();
            }
        }
        
        private async Task<Response> MakeRequest(Request request)
        {
            var stopwatch = Stopwatch.StartNew();
            
            using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
            
            foreach (var header in request.Headers)
            {
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            if (!string.IsNullOrEmpty(request.Body))
            {
                httpRequest.Content = new StringContent(request.Body);
            }
            
            var httpResponse = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.SendAsync(httpRequest);
            });
            
            var body = await httpResponse.Content.ReadAsStringAsync();
            stopwatch.Stop();
            
            var response = new Response
            {
                StatusCode = httpResponse.StatusCode,
                Body = body,
                Url = request.Url,
                Request = request,
                ResponseTime = stopwatch.Elapsed,
                ContentType = httpResponse.Content.Headers.ContentType?.MediaType,
                ContentLength = httpResponse.Content.Headers.ContentLength
            };
            
            foreach (var header in httpResponse.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }
            
            foreach (var header in httpResponse.Content.Headers)
            {
                response.Headers[header.Key] = string.Join(", ", header.Value);
            }
            
            if (_options.ParseHtml && _browsingContext != null)
            {
                var contentType = httpResponse.Content.Headers.ContentType?.MediaType;
                if (contentType?.Contains("text/html") == true)
                {
                    response.Html = await _browsingContext.OpenAsync(req => req.Content(body).Address(request.Url)) as IHtmlDocument;
                }
            }
            
            if (_options.ParseJson)
            {
                var contentType = httpResponse.Content.Headers.ContentType?.MediaType;
                if (contentType?.Contains("application/json") == true || contentType?.Contains("text/json") == true)
                {
                    try
                    {
                        response.Json = JsonDocument.Parse(body);
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "Failed to parse JSON response from: {Url}", request.Url);
                    }
                }
            }
            
            return response;
        }
        
        public Statistics GetStatistics() => _statistics;
        
        public void Stop()
        {
            _isRunning = false;
        }
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            _autoscaledPool?.Dispose();
        }
    }
}