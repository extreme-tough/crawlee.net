using Crawlee.NET.Storage;
using Xunit;

namespace Crawlee.NET.Tests
{
    public class StorageTests
    {
        [Fact]
        public async Task MemoryDataset_ShouldStoreAndRetrieveData()
        {
            // Arrange
            var dataset = new MemoryDataset();
            var testData = new { Name = "Test", Value = 123 };
            
            // Act
            await dataset.PushData(testData);
            var retrievedData = await dataset.GetData&lt;dynamic&gt;();
            
            // Assert
            Assert.Single(retrievedData);
            Assert.Equal(1, await dataset.GetItemCount());
        }
        
        [Fact]
        public async Task MemoryKeyValueStore_ShouldStoreAndRetrieveValues()
        {
            // Arrange
            var store = new MemoryKeyValueStore();
            var key = "test-key";
            var value = new { Message = "Hello World" };
            
            // Act
            await store.SetValue(key, value);
            var retrieved = await store.GetValue&lt;dynamic&gt;(key);
            var hasKey = await store.HasKey(key);
            
            // Assert
            Assert.NotNull(retrieved);
            Assert.True(hasKey);
        }
        
        [Fact]
        public async Task MemoryDataset_ShouldSupportPagination()
        {
            // Arrange
            var dataset = new MemoryDataset();
            var items = Enumerable.Range(1, 10).Select(i =&gt; new { Id = i });
            
            // Act
            await dataset.PushData(items);
            var page1 = await dataset.GetData&lt;dynamic&gt;(limit: 3, offset: 0);
            var page2 = await dataset.GetData&lt;dynamic&gt;(limit: 3, offset: 3);
            
            // Assert
            Assert.Equal(3, page1.Count());
            Assert.Equal(3, page2.Count());
            Assert.Equal(10, await dataset.GetItemCount());
        }
    }
}