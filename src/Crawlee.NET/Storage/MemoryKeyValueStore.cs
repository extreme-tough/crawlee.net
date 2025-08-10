using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public class MemoryKeyValueStore : IKeyValueStore
    {
        private readonly ConcurrentDictionary<string, string> _store = new();
        
        public Task SetValue(string key, object value)
        {
            var json = JsonConvert.SerializeObject(value);
            _store.AddOrUpdate(key, json, (_, _) => json);
            return Task.CompletedTask;
        }
        
        public Task<T?> GetValue<T>(string key)
        {
            if (_store.TryGetValue(key, out var json))
            {
                return Task.FromResult(JsonConvert.DeserializeObject<T>(json));
            }
            return Task.FromResult(default(T));
        }
        
        public Task<bool> HasKey(string key)
        {
            return Task.FromResult(_store.ContainsKey(key));
        }
        
        public Task Delete(string key)
        {
            _store.TryRemove(key, out _);
            return Task.CompletedTask;
        }
        
        public Task<IEnumerable<string>> GetKeys()
        {
            return Task.FromResult(_store.Keys.AsEnumerable());
        }
    }
}