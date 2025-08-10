using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public interface IDataset
    {
        Task PushData(object data);
        Task PushData(IEnumerable&lt;object&gt; data);
        Task&lt;IEnumerable&lt;T&gt;&gt; GetData&lt;T&gt;(int? limit = null, int offset = 0);
        Task Clear();
        Task&lt;int&gt; GetItemCount();
    }
}