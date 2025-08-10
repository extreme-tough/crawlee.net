using Crawlee.NET.Storage;

namespace Crawlee.NET.Models
{
    public class CrawlingContext
    {
        public Request Request { get; set; }
        public Response Response { get; set; }
        public IDataset Dataset { get; set; }
        public IKeyValueStore KeyValueStore { get; set; }
        public Session? Session { get; set; }
        public Dictionary&lt;string, object&gt; State { get; set; } = new();
        
        public CrawlingContext(Request request, Response response, IDataset dataset, IKeyValueStore keyValueStore)
        {
            Request = request;
            Response = response;
            Dataset = dataset;
            KeyValueStore = keyValueStore;
        }
        
        public async Task EnqueueLinks(IEnumerable&lt;string&gt; urls, Dictionary&lt;string, object&gt;? userData = null)
        {
            var requests = urls.Select(url =&gt; new Request(url, userData));
            // This would be injected in a real implementation
            await Task.CompletedTask; // Placeholder
        }
    }
}