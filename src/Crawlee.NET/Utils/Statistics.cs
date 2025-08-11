using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Crawlee.NET.Utils
{
    public class Statistics
    {
        private readonly ConcurrentDictionary<string, long> _counters = new();
        private readonly object _lock = new();
        private DateTime _startTime = DateTime.UtcNow;
        
        public long RequestsFinished => GetCounter("requestsFinished");
        public long RequestsFailed => GetCounter("requestsFailed");
        public long RequestsRetries => GetCounter("requestsRetries");
        public long RequestsFailedPerMinute => GetCounter("requestsFailedPerMinute");
        public long RequestsFinishedPerMinute => GetCounter("requestsFinishedPerMinute");
        public long RequestMinDurationMillis => GetCounter("requestMinDurationMillis");
        public long RequestMaxDurationMillis => GetCounter("requestMaxDurationMillis");
        public long RequestTotalDurationMillis => GetCounter("requestTotalDurationMillis");
        public double RequestAvgDurationMillis => RequestsFinished > 0 ? (double)RequestTotalDurationMillis / RequestsFinished : 0;
        
        public TimeSpan CrawlerRuntimeMillis => DateTime.UtcNow - _startTime;
        
        public void IncrementCounter(string key, long value = 1)
        {
            _counters.AddOrUpdate(key, value, (k, v) => v + value);
        }
        
        public long GetCounter(string key)
        {
            return _counters.GetValueOrDefault(key, 0);
        }
        
        public void SetCounter(string key, long value)
        {
            _counters.AddOrUpdate(key, value, (k, v) => value);
        }
        
        public void RecordRequestDuration(long durationMillis)
        {
            lock (_lock)
            {
                IncrementCounter("requestTotalDurationMillis", durationMillis);
                
                var currentMin = GetCounter("requestMinDurationMillis");
                if (currentMin == 0 || durationMillis < currentMin)
                {
                    SetCounter("requestMinDurationMillis", durationMillis);
                }
                
                var currentMax = GetCounter("requestMaxDurationMillis");
                if (durationMillis > currentMax)
                {
                    SetCounter("requestMaxDurationMillis", durationMillis);
                }
            }
        }
        
        public void Reset()
        {
            _counters.Clear();
            _startTime = DateTime.UtcNow;
        }
        
        public StatisticsSnapshot GetSnapshot()
        {
            return new StatisticsSnapshot
            {
                RequestsFinished = RequestsFinished,
                RequestsFailed = RequestsFailed,
                RequestsRetries = RequestsRetries,
                RequestAvgDurationMillis = RequestAvgDurationMillis,
                RequestMinDurationMillis = RequestMinDurationMillis,
                RequestMaxDurationMillis = RequestMaxDurationMillis,
                CrawlerRuntimeMillis = CrawlerRuntimeMillis,
                RequestsPerMinute = CrawlerRuntimeMillis.TotalMinutes > 0 ? RequestsFinished / CrawlerRuntimeMillis.TotalMinutes : 0
            };
        }
    }
    
    public class StatisticsSnapshot
    {
        public long RequestsFinished { get; set; }
        public long RequestsFailed { get; set; }
        public long RequestsRetries { get; set; }
        public double RequestAvgDurationMillis { get; set; }
        public long RequestMinDurationMillis { get; set; }
        public long RequestMaxDurationMillis { get; set; }
        public TimeSpan CrawlerRuntimeMillis { get; set; }
        public double RequestsPerMinute { get; set; }
    }
}