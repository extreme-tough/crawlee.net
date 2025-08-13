using Crawlee.NET.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Crawlee.NET.Queue
{
    public class MemoryRequestQueue : IRequestQueue
    {
        private readonly ConcurrentDictionary<string, Request> _requests = new();
        private readonly ConcurrentQueue<string> _pendingRequestIds = new();
        private readonly HashSet<string> _handledRequestIds = new();
        private readonly HashSet<string> _inProgressRequestIds = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        
        public async Task AddRequest(Request request)
        {
            var key = request.GetUniqueKey();
            if (_requests.ContainsKey(key) || _handledRequestIds.Contains(key))
                return; // Duplicate request
                
            _requests.TryAdd(key, request);
            _pendingRequestIds.Enqueue(key);
            await Task.CompletedTask;
        }
        
        public async Task AddRequests(IEnumerable<Request> requests)
        {
            foreach (var request in requests)
            {
                await AddRequest(request);
            }
        }
        
        public async Task<Request?> FetchNextRequest()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!_pendingRequestIds.TryDequeue(out var requestId))
                    return null;
                    
                if (!_requests.TryGetValue(requestId, out var request))
                    return null;
                    
                _inProgressRequestIds.Add(requestId);
                return request;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task MarkRequestHandled(Request request)
        {
            var key = request.GetUniqueKey();
            await _semaphore.WaitAsync();
            try
            {
                _inProgressRequestIds.Remove(key);
                _handledRequestIds.Add(key);
                _requests.TryRemove(key, out _);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task ReclaimRequest(Request request)
        {
            var key = request.GetUniqueKey();
            await _semaphore.WaitAsync();
            try
            {
                if (_inProgressRequestIds.Remove(key))
                {
                    _pendingRequestIds.Enqueue(key);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task<bool> IsEmpty()
        {
            await Task.CompletedTask;
            return _pendingRequestIds.IsEmpty && _inProgressRequestIds.Count == 0;
        }
        
        public async Task<int> GetTotalCount()
        {
            await Task.CompletedTask;
            return _requests.Count + _handledRequestIds.Count;
        }
        
        public async Task<int> GetHandledCount()
        {
            await Task.CompletedTask;
            return _handledRequestIds.Count;
        }
    }
}