using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.IO;

namespace Crawlee.NET.Models
{
    public class Response
    {
        public HttpStatusCode StatusCode { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new();
        public string Body { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public IHtmlDocument? Html { get; set; }
        public JsonDocument? Json { get; set; }
        public byte[]? Buffer { get; set; }
        public bool IsSuccess => (int)StatusCode >= 200 && (int)StatusCode < 300;
        public Request Request { get; set; } = new();
        public string? ContentType { get; set; }
        public long? ContentLength { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public string? RedirectedFrom { get; set; }
        public List<string> RedirectChain { get; set; } = new();
        public string? Encoding { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        
        public T? JsonAs<T>() where T : class
        {
            if (Json == null) return null;
            return JsonSerializer.Deserialize<T>(Json.RootElement.GetRawText());
        }
        
        public async Task<Stream> GetStreamAsync()
        {
            if (Buffer != null)
                return new MemoryStream(Buffer);
                
            return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Body));
        }
        
        public string GetText(string? encoding = null)
        {
            if (!string.IsNullOrEmpty(Body))
                return Body;
                
            if (Buffer != null)
            {
                var enc = encoding != null ? System.Text.Encoding.GetEncoding(encoding) : System.Text.Encoding.UTF8;
                return enc.GetString(Buffer);
            }
            
            return string.Empty;
        }
    }
}