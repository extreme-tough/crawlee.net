using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Crawlee.NET.Utils
{
    public class ProxyConfiguration
    {
        private readonly List<ProxyInfo> _proxies = new();
        private readonly Random _random = new();
        private int _currentIndex = 0;
        
        public ProxyConfiguration(IEnumerable<string> proxyUrls)
        {
            foreach (var url in proxyUrls)
            {
                _proxies.Add(new ProxyInfo(url));
            }
        }
        
        public ProxyInfo? GetNextProxy()
        {
            if (!_proxies.Any())
                return null;
                
            var availableProxies = _proxies.Where(p => !p.IsBlocked).ToList();
            if (!availableProxies.Any())
                return null;
                
            // Round-robin selection
            var proxy = availableProxies[_currentIndex % availableProxies.Count];
            _currentIndex++;
            
            return proxy;
        }
        
        public ProxyInfo? GetRandomProxy()
        {
            var availableProxies = _proxies.Where(p => !p.IsBlocked).ToList();
            if (!availableProxies.Any())
                return null;
                
            var index = _random.Next(availableProxies.Count);
            return availableProxies[index];
        }
        
        public void MarkProxyBad(ProxyInfo proxy, string? reason = null)
        {
            proxy.ErrorCount++;
            proxy.LastError = reason;
            proxy.LastUsed = DateTime.UtcNow;
        }
        
        public void MarkProxyGood(ProxyInfo proxy)
        {
            proxy.ErrorCount = Math.Max(0, proxy.ErrorCount - 1);
            proxy.LastUsed = DateTime.UtcNow;
        }
    }
    
    public class ProxyInfo
    {
        public string Url { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public int ErrorCount { get; set; } = 0;
        public int MaxErrors { get; set; } = 5;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;
        public string? LastError { get; set; }
        
        public bool IsBlocked => ErrorCount >= MaxErrors;
        
        public ProxyInfo(string url)
        {
            Url = url;
            
            // Parse username/password from URL if present
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(':');
                    if (parts.Length >= 1) Username = parts[0];
                    if (parts.Length >= 2) Password = parts[1];
                }
            }
        }
        
        public WebProxy ToWebProxy()
        {
            var proxy = new WebProxy(Url);
            if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
            {
                proxy.Credentials = new NetworkCredential(Username, Password);
            }
            return proxy;
        }
    }
}