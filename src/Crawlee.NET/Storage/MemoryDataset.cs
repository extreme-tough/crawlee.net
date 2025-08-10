using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public class MemoryDataset : IDataset
    {
        private readonly ConcurrentQueue&lt;string&gt; _data = new();
        
        public Task PushData(object data)
        {
            var json = JsonConvert.SerializeObject(data);
            _data.Enqueue(json);
            return Task.CompletedTask;
        }
        
        public Task PushData(IEnumerable&lt;object&gt; data)
        {
            foreach (var item in data)
            {
                var json = JsonConvert.SerializeObject(item);
                _data.Enqueue(json);
            }
            return Task.CompletedTask;
        }
        
        public Task&lt;IEnumerable&lt;T&gt;&gt; GetData&lt;T&gt;(int? limit = null, int offset = 0)
        {
            var items = _data.Skip(offset);
            if (limit.HasValue)
                items = items.Take(limit.Value);
                
            var result = items.Select(json =&gt; JsonConvert.DeserializeObject&lt;T&gt;(json)!)
                             .Where(item =&gt; item != null);
            return Task.FromResult(result);
        }
        
        public Task Clear()
        {
            while (_data.TryDequeue(out _)) { }
            return Task.CompletedTask;
        }
        
        public Task&lt;int&gt; GetItemCount()
        {
            return Task.FromResult(_data.Count);
        }
    }
}