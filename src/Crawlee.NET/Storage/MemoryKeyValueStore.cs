using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly ConcurrentDictionary&lt;string, string&gt; _store = new();
        
        public Task SetValue(string key, object value)
        {
            var json = JsonConvert.SerializeObject(value);
            _store.AddOrUpdate(key, json, (_, _) =&gt; json);
            return Task.CompletedTask;
        }
        
        public Task&lt;T?&gt; GetValue&lt;T&gt;(string key)
        {
            if (_store.TryGetValue(key, out var json))
            {
                return Task.FromResult(JsonConvert.DeserializeObject&lt;T&gt;(json));
            }
            return Task.FromResult(default(T));
        }
        
        public Task&lt;bool&gt; HasKey(string key)
        {
            return Task.FromResult(_store.ContainsKey(key));
        }
        
        public Task Delete(string key)
        {
            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }
        
        public Task&lt;IEnumerable&lt;string&gt;&gt; GetKeys()
        {
            return Task.FromResult(_store.Keys.AsEnumerable());
        }
    }
}