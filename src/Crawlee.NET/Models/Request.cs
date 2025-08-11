using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Crawlee.NET.Models
{
    public class Request
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
        
        [JsonPropertyName("uniqueKey")]
        public string UniqueKey { get; set; } = string.Empty;
        
        [JsonPropertyName("method")]
        public string Method { get; set; } = "GET";
        
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new();
        
        [JsonPropertyName("payload")]
        public string? Body { get; set; }
        
        [JsonPropertyName("userData")]
        public Dictionary<string, object> UserData { get; set; } = new();
        
        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 0;
        
        [JsonPropertyName("retryCount")]
        public int RetryCount { get; set; } = 0;
        
        [JsonPropertyName("maxRetries")]
        public int MaxRetries { get; set; } = 3;
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        [JsonPropertyName("handledAt")]
        public DateTime? HandledAt { get; set; }
        
        [JsonPropertyName("label")]
        public string? Label { get; set; }
        
        [JsonPropertyName("skipNavigation")]
        public bool SkipNavigation { get; set; } = false;
        
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }
        
        [JsonPropertyName("proxyUrl")]
        public string? ProxyUrl { get; set; }
        
        [JsonPropertyName("loadedUrl")]
        public string? LoadedUrl { get; set; }
        
        [JsonPropertyName("errorMessages")]
        public List<string> ErrorMessages { get; set; } = new();
        
        public Request() { }
        
        public Request(string url, Dictionary<string, object>? userData = null, string? uniqueKey = null)
        {
            Url = url;
            UniqueKey = uniqueKey ?? url;
            if (userData != null)
                UserData = userData;
        }
        
        public void PushErrorMessage(string errorMessage)
        {
            ErrorMessages.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: {errorMessage}");
        }
    }
}