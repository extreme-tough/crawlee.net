using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public class FileDataset : IDataset
    {
        private readonly string _datasetPath;
        private readonly object _lock = new();
        private int _itemCount = 0;
        
        public FileDataset(string? datasetPath = null)
        {
            _datasetPath = datasetPath ?? Path.Combine(Environment.CurrentDirectory, "storage", "datasets", "default");
            Directory.CreateDirectory(_datasetPath);
            
            // Count existing items
            var files = Directory.GetFiles(_datasetPath, "*.json");
            _itemCount = files.Length;
        }
        
        public Task PushData(object data)
        {
            lock (_lock)
            {
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                var fileName = $"{_itemCount:D6}.json";
                var filePath = Path.Combine(_datasetPath, fileName);
                
                File.WriteAllText(filePath, json);
                _itemCount++;
            }
            
            return Task.CompletedTask;
        }
        
        public Task PushData(IEnumerable<object> data)
        {
            foreach (var item in data)
            {
                PushData(item).Wait();
            }
            return Task.CompletedTask;
        }
        
        public Task<IEnumerable<T>> GetData<T>(int? limit = null, int offset = 0)
        {
            var files = Directory.GetFiles(_datasetPath, "*.json")
                .OrderBy(f => f)
                .Skip(offset);
                
            if (limit.HasValue)
                files = files.Take(limit.Value);
                
            var result = files.Select(file =>
            {
                var json = File.ReadAllText(file);
                return JsonConvert.DeserializeObject<T>(json)!;
            }).Where(item => item != null);
            
            return Task.FromResult(result);
        }
        
        public Task Clear()
        {
            lock (_lock)
            {
                var files = Directory.GetFiles(_datasetPath, "*.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
                _itemCount = 0;
            }
            
            return Task.CompletedTask;
        }
        
        public Task<int> GetItemCount()
        {
            return Task.FromResult(_itemCount);
        }
    }
}