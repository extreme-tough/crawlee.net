using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public class FileKeyValueStore : IKeyValueStore
    {
        private readonly string _storePath;
        private readonly object _lock = new();
        
        public FileKeyValueStore(string? storePath = null)
        {
            _storePath = storePath ?? Path.Combine(Environment.CurrentDirectory, "storage", "key_value_stores", "default");
            Directory.CreateDirectory(_storePath);
        }
        
        public Task SetValue(string key, object value)
        {
            lock (_lock)
            {
                var json = JsonConvert.SerializeObject(value, Formatting.Indented);
                var fileName = SanitizeFileName(key) + ".json";
                var filePath = Path.Combine(_storePath, fileName);
                
                File.WriteAllText(filePath, json);
            }
            
            return Task.CompletedTask;
        }
        
        public Task<T?> GetValue<T>(string key)
        {
            var fileName = SanitizeFileName(key) + ".json";
            var filePath = Path.Combine(_storePath, fileName);
            
            if (!File.Exists(filePath))
                return Task.FromResult(default(T));
                
            var json = File.ReadAllText(filePath);
            var value = JsonConvert.DeserializeObject<T>(json);
            return Task.FromResult(value);
        }
        
        public Task<bool> HasKey(string key)
        {
            var fileName = SanitizeFileName(key) + ".json";
            var filePath = Path.Combine(_storePath, fileName);
            return Task.FromResult(File.Exists(filePath));
        }
        
        public Task Delete(string key)
        {
            lock (_lock)
            {
                var fileName = SanitizeFileName(key) + ".json";
                var filePath = Path.Combine(_storePath, fileName);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            
            return Task.CompletedTask;
        }
        
        public Task<IEnumerable<string>> GetKeys()
        {
            var files = Directory.GetFiles(_storePath, "*.json");
            var keys = files.Select(f => Path.GetFileNameWithoutExtension(f))
                           .Where(k => !string.IsNullOrEmpty(k))
                           .Select(k => k!);
            
            return Task.FromResult(keys);
        }
        
        private static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}