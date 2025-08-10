using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public interface IKeyValueStore
    {
        Task SetValue(string key, object value);
        Task<T?> GetValue<T>(string key);
        Task<bool> HasKey(string key);
        Task Delete(string key);
        Task<IEnumerable<string>> GetKeys();
    }
}