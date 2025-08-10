using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public interface IKeyValueStore
    {
        Task SetValue(string key, object value);
        Task&lt;T?&gt; GetValue&lt;T&gt;(string key);
        Task&lt;bool&gt; HasKey(string key);
        Task Delete(string key);
        Task&lt;IEnumerable&lt;string&gt;&gt; GetKeys();
    }
}