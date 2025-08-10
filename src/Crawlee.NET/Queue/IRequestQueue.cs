using Crawlee.NET.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crawlee.NET.Queue
{
    public interface IRequestQueue
    {
        Task AddRequest(Request request);
        Task AddRequests(IEnumerable<Request> requests);
        Task<Request?> FetchNextRequest();
        Task MarkRequestHandled(Request request);
        Task ReclaimRequest(Request request);
        Task<bool> IsEmpty();
        Task<int> GetTotalCount();
        Task<int> GetHandledCount();
    }
}