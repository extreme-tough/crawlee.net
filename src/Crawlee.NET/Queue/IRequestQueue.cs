using Crawlee.NET.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crawlee.NET.Queue
{
    public interface IRequestQueue
    {
        Task AddRequest(Request request);
        Task AddRequests(IEnumerable&lt;Request&gt; requests);
        Task&lt;Request?&gt; FetchNextRequest();
        Task MarkRequestHandled(Request request);
        Task ReclaimRequest(Request request);
        Task&lt;bool&gt; IsEmpty();
        Task&lt;int&gt; GetTotalCount();
        Task&lt;int&gt; GetHandledCount();
    }
}