using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crawlee.NET.Storage
{
    public interface IDataset
    {
        Task PushData(object data);
        Task PushData(IEnumerable<object> data);
        Task<IEnumerable<T>> GetData<T>(int? limit = null, int offset = 0);
        Task Clear();
        Task<int> GetItemCount();
    }
}