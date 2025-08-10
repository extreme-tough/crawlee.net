using System;
using System.Collections.Generic;

namespace Crawlee.NET.Models
{
    public class Session
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Dictionary<string, string> Cookies { get; set; } = new();
        public Dictionary<string, string> Headers { get; set; } = new();
        public string? ProxyUrl { get; set; }
        public int ErrorCount { get; set; } = 0;
        public int MaxErrorCount { get; set; } = 5;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public bool IsBlocked => ErrorCount >= MaxErrorCount;
        
        public void MarkErrored()
        {
            ErrorCount++;
            LastUsed = DateTime.UtcNow;
        }
        
        public void MarkSuccessful()
        {
            ErrorCount = Math.Max(0, ErrorCount - 1);
            LastUsed = DateTime.UtcNow;
        }
    }
}