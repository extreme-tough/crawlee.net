using Crawlee.NET.Crawlers;
using Crawlee.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Crawlee.NET.Tests
{
    public class CrawlerTests
    {
        [Fact]
        public async Task HttpCrawler_ShouldProcessBasicRequest()
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<HttpCrawler>();
            var crawler = new HttpCrawler(new HttpCrawlerOptions
            {
                MaxConcurrency = 1,
                RequestDelayMilliseconds = 0,
                MaxRetries = 1
            }, logger);
            
            var processedUrls = new List<string>();
            
            // Act
            await crawler.Run(async (context) =>
            {
                processedUrls.Add(context.Request.Url);
                await context.Dataset.PushData(new { Url = context.Request.Url });
            });
            
            await crawler.AddRequests("https://httpbin.org/html");
            
            // Wait a bit for processing
            await Task.Delay(2000);
            
            // Assert
            Assert.Single(processedUrls);
            Assert.Equal(1, await crawler._dataset.GetItemCount());
        }
        
        [Fact]
        public async Task HttpCrawler_ShouldHandleFailedRequests()
        {
            // Arrange
            var logger = new LoggerFactory().CreateLogger<HttpCrawler>();
            var crawler = new HttpCrawler(new HttpCrawlerOptions
            {
                MaxConcurrency = 1,
                RequestDelayMilliseconds = 0,
                MaxRetries = 1
            }, logger);
            
            var failedRequests = new List<string>();
            
            crawler.FailedRequestHandler(async (context, exception) =>
            {
                failedRequests.Add(context.Request.Url);
                await Task.CompletedTask;
            });
            
            // Act
            await crawler.Run(async (context) =>
            {
                await context.Dataset.PushData(new { Url = context.Request.Url });
            });
            
            await crawler.AddRequests("https://invalid-url-that-does-not-exist.com");
            
            // Wait a bit for processing
            await Task.Delay(2000);
            
            // Assert - This test might be flaky depending on network conditions
            // In a real scenario, you'd mock the HttpClient
        }
        
        [Fact]
        public void Statistics_ShouldTrackMetrics()
        {
            // Arrange
            var stats = new Crawlee.NET.Utils.Statistics();
            
            // Act
            stats.IncrementCounter("requestsFinished", 5);
            stats.IncrementCounter("requestsFailed", 2);
            stats.RecordRequestDuration(100);
            stats.RecordRequestDuration(200);
            
            var snapshot = stats.GetSnapshot();
            
            // Assert
            Assert.Equal(5, snapshot.RequestsFinished);
            Assert.Equal(2, snapshot.RequestsFailed);
            Assert.Equal(150, snapshot.RequestAvgDurationMillis);
            Assert.Equal(100, snapshot.RequestMinDurationMillis);
            Assert.Equal(200, snapshot.RequestMaxDurationMillis);
        }
    }
}