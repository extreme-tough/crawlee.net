using Crawlee.NET.Storage;
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
        public Session? Session { get; set; }
        public Dictionary<string, object> State { get; set; } = new();
        
        public CrawlingContext(Request request, Response response, IDataset dataset, IKeyValueStore keyValueStore)
        {
            Request = request;
            Response = response;
            Dataset = dataset;
            KeyValueStore = keyValueStore;
        }
        
        public async Task EnqueueLinks(IEnumerable<string> urls, Dictionary<string, object>? userData = null)
        {
            var requests = urls.Select(url => new Request(url, userData));
            // This would be injected in a real implementation
            await Task.CompletedTask; // Placeholder
        }
    }
}