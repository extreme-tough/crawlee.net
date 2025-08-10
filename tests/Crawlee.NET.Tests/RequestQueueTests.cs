using Crawlee.NET.Models;
using Crawlee.NET.Queue;
using Xunit;

namespace Crawlee.NET.Tests
{
    public class RequestQueueTests
    {
        [Fact]
        public async Task RequestQueue_ShouldAddAndFetchRequests()
        {
            // Arrange
            var queue = new MemoryRequestQueue();
            var request = new Request("https://example.com");
            
            // Act
            await queue.AddRequest(request);
            var fetchedRequest = await queue.FetchNextRequest();
            
            // Assert
            Assert.NotNull(fetchedRequest);
            Assert.Equal(request.Url, fetchedRequest.Url);
        }
        
        [Fact]
        public async Task RequestQueue_ShouldPreventDuplicateRequests()
        {
            // Arrange
            var queue = new MemoryRequestQueue();
            var request1 = new Request("https://example.com");
            var request2 = new Request("https://example.com"); // Duplicate
            
            // Act
            await queue.AddRequest(request1);
            await queue.AddRequest(request2);
            
            var fetched1 = await queue.FetchNextRequest();
            var fetched2 = await queue.FetchNextRequest();
            
            // Assert
            Assert.NotNull(fetched1);
            Assert.Null(fetched2); // Should be null due to deduplication
        }
        
        [Fact]
        public async Task RequestQueue_ShouldTrackHandledRequests()
        {
            // Arrange
            var queue = new MemoryRequestQueue();
            var request = new Request("https://example.com");
            
            // Act
            await queue.AddRequest(request);
            var fetched = await queue.FetchNextRequest();
            await queue.MarkRequestHandled(fetched!);
            
            // Assert
            Assert.Equal(1, await queue.GetHandledCount());
            Assert.True(await queue.IsEmpty());
        }
    }
}