using System.Collections.Generic;

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
    }
}