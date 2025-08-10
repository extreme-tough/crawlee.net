using System;
using System.Collections.Generic;

namespace Crawlee.NET.Models
{
    public class Request
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? Body { get; set; }
        public Dictionary<string, object> UserData { get; set; } = new();
        public int Priority { get; set; } = 0;
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Label { get; set; }
        public bool SkipNavigation { get; set; } = false;
        
        public Request() { }
        
        public Request(string url, Dictionary<string, object>? userData = null)
        {
            Url = url;
            if (userData != null)
                UserData = userData;
        }
    }
}