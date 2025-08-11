using Crawlee.NET.Storage;
using Crawlee.NET.Queue;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crawlee.NET.Models
{
    public class CrawlingContext
    {
        public Request Request { get; set; }
        public Response Response { get; set; }
        public IDataset Dataset { get; set; }
        public IKeyValueStore KeyValueStore { get; set; }
        public IRequestQueue RequestQueue { get; set; }
        public Session? Session { get; set; }
        public Dictionary<string, object> State { get; set; } = new();
        public ILogger? Logger { get; set; }
        public string? ProxyInfo { get; set; }
        public Dictionary<string, object> GlobalState { get; set; } = new();
        
        public CrawlingContext(
            Request request, 
            Response response, 
            IDataset dataset, 
            IKeyValueStore keyValueStore,
            IRequestQueue requestQueue,
            ILogger? logger = null)
        {
            Request = request;
            Response = response;
            Dataset = dataset;
            KeyValueStore = keyValueStore;
            RequestQueue = requestQueue;
            Logger = logger;
        }
        
        public async Task EnqueueLinks(
            IEnumerable<string> urls, 
            Dictionary<string, object>? userData = null,
            string? label = null,
            int priority = 0)
        {
            var requests = urls.Select(url => new Request(url, userData)
            {
                Label = label,
                Priority = priority
            });
            
            await RequestQueue.AddRequests(requests);
        }
        
        public async Task EnqueueLink(
            string url, 
            Dictionary<string, object>? userData = null,
            string? label = null,
            int priority = 0)
        {
            var request = new Request(url, userData)
            {
                Label = label,
                Priority = priority
            };
            
            await RequestQueue.AddRequest(request);
        }
        
        public void Log(string message, params object[] args)
        {
            Logger?.LogInformation(message, args);
        }
        
        public void LogWarning(string message, params object[] args)
        {
            Logger?.LogWarning(message, args);
        }
        
        public void LogError(string message, params object[] args)
        {
            Logger?.LogError(message, args);
        }
    }
}