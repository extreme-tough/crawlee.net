using System.Collections.Generic;
using System;

namespace Crawlee.NET.Crawlers
{
    public class CrawlerOptions
    {
        public int MaxConcurrency { get; set; } = 10;
        public int RequestDelayMilliseconds { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public int RequestTimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string> DefaultHeaders { get; set; } = new();
        public bool IgnoreHttpsErrors { get; set; } = false;
        public string UserAgent { get; set; } = "Crawlee.NET/1.0";
        public bool UseSessionPool { get; set; } = true;
        public int MaxSessionPoolSize { get; set; } = 1000;
        public bool PersistCookiesPerSession { get; set; } = true;
        public int RetryDelayMilliseconds { get; set; } = 1000;
        public int MaxRequestsPerCrawl { get; set; } = int.MaxValue;
        public TimeSpan? MaxCrawlTime { get; set; }
        public bool AutoscaledPoolOptions { get; set; } = true;
        public int MinConcurrency { get; set; } = 1;
        public double SystemStatusMaxRatio { get; set; } = 0.95;
        public bool LogLevel { get; set; } = true;
        public string? ProxyConfiguration { get; set; }
        public bool KeepAlive { get; set; } = true;
        public Dictionary<string, object> LaunchContext { get; set; } = new();
        public bool HandleFailedRequestFunction { get; set; } = true;
        public int MaxRequestRetries { get; set; } = 3;
        public TimeSpan RequestHandlerTimeoutSecs { get; set; } = TimeSpan.FromMinutes(1);
        public bool RecycleDiskCache { get; set; } = false;
        public string? SessionPoolOptions { get; set; }
        public bool UseExtendedUniqueKey { get; set; } = false;
        public int MaxRedirects { get; set; } = 20;
        public List<string> AdditionalMimeTypes { get; set; } = new();
        public Dictionary<string, string> SuggestResponseEncoding { get; set; } = new();
        public bool ForceResponseEncoding { get; set; } = false;
    }
}