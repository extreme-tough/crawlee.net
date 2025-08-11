using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Crawlee.NET.Utils
{
    public class AutoscaledPoolOptions
    {
        public int MinConcurrency { get; set; } = 1;
        public int MaxConcurrency { get; set; } = 200;
        public int DesiredConcurrency { get; set; } = 10;
        public double ScaleUpStepRatio { get; set; } = 0.05;
        public double ScaleDownStepRatio { get; set; } = 0.05;
        public TimeSpan AutoscaleInterval { get; set; } = TimeSpan.FromSeconds(10);
        public double CpuSnapshotInterval { get; set; } = 1.0;
        public double MaxMemoryRatio { get; set; } = 0.7;
        public double MaxCpuRatio { get; set; } = 0.4;
        public bool LogLevel { get; set; } = true;
    }

    public class AutoscaledPool
    {
        private readonly AutoscaledPoolOptions _options;
        private readonly ILogger<AutoscaledPool>? _logger;
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly ConcurrentQueue<Func<Task>> _taskQueue = new();
        private readonly Timer _autoscaleTimer;
        private readonly PerformanceCounter? _cpuCounter;
        
        private int _currentConcurrency;
        private int _runningTasks;
        private bool _isRunning;
        private readonly object _scalingLock = new();
        
        public int CurrentConcurrency => _currentConcurrency;
        public int RunningTasks => _runningTasks;
        public bool IsRunning => _isRunning;
        
        public AutoscaledPool(AutoscaledPoolOptions? options = null, ILogger<AutoscaledPool>? logger = null)
        {
            _options = options ?? new AutoscaledPoolOptions();
            _logger = logger;
            _currentConcurrency = _options.DesiredConcurrency;
            _concurrencySemaphore = new SemaphoreSlim(_currentConcurrency, _options.MaxConcurrency);
            
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not initialize CPU performance counter");
            }
            
            _autoscaleTimer = new Timer(AutoscaleCallback, null, _options.AutoscaleInterval, _options.AutoscaleInterval);
        }
        
        public async Task AddTask(Func<Task> taskFactory)
        {
            if (!_isRunning) return;
            
            _taskQueue.Enqueue(taskFactory);
            await ProcessQueue();
        }
        
        public void Start()
        {
            _isRunning = true;
            _logger?.LogInformation("AutoscaledPool started with concurrency: {Concurrency}", _currentConcurrency);
        }
        
        public void Stop()
        {
            _isRunning = false;
            _autoscaleTimer?.Dispose();
            _logger?.LogInformation("AutoscaledPool stopped");
        }
        
        private async Task ProcessQueue()
        {
            while (_taskQueue.TryDequeue(out var taskFactory) && _isRunning)
            {
                await _concurrencySemaphore.WaitAsync();
                
                if (!_isRunning)
                {
                    _concurrencySemaphore.Release();
                    break;
                }
                
                _ = Task.Run(async () =>
                {
                    Interlocked.Increment(ref _runningTasks);
                    try
                    {
                        await taskFactory();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Task execution failed in AutoscaledPool");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _runningTasks);
                        _concurrencySemaphore.Release();
                    }
                });
            }
        }
        
        private void AutoscaleCallback(object? state)
        {
            if (!_isRunning) return;
            
            lock (_scalingLock)
            {
                try
                {
                    var systemLoad = GetSystemLoad();
                    var queueLength = _taskQueue.Count;
                    var shouldScaleUp = queueLength > 0 && systemLoad.CpuUsage < _options.MaxCpuRatio && systemLoad.MemoryUsage < _options.MaxMemoryRatio;
                    var shouldScaleDown = queueLength == 0 && _runningTasks < _currentConcurrency * 0.3;
                    
                    if (shouldScaleUp && _currentConcurrency < _options.MaxConcurrency)
                    {
                        var increase = Math.Max(1, (int)(_currentConcurrency * _options.ScaleUpStepRatio));
                        var newConcurrency = Math.Min(_options.MaxConcurrency, _currentConcurrency + increase);
                        
                        if (newConcurrency > _currentConcurrency)
                        {
                            _concurrencySemaphore.Release(newConcurrency - _currentConcurrency);
                            _currentConcurrency = newConcurrency;
                            _logger?.LogDebug("Scaled up concurrency to: {Concurrency}", _currentConcurrency);
                        }
                    }
                    else if (shouldScaleDown && _currentConcurrency > _options.MinConcurrency)
                    {
                        var decrease = Math.Max(1, (int)(_currentConcurrency * _options.ScaleDownStepRatio));
                        var newConcurrency = Math.Max(_options.MinConcurrency, _currentConcurrency - decrease);
                        
                        if (newConcurrency < _currentConcurrency)
                        {
                            // Note: We can't easily reduce semaphore count, so we just track the logical concurrency
                            _currentConcurrency = newConcurrency;
                            _logger?.LogDebug("Scaled down concurrency to: {Concurrency}", _currentConcurrency);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during autoscaling");
                }
            }
        }
        
        private SystemLoad GetSystemLoad()
        {
            var cpuUsage = 0.0;
            var memoryUsage = 0.0;
            
            try
            {
                if (_cpuCounter != null)
                {
                    cpuUsage = _cpuCounter.NextValue() / 100.0;
                }
                
                var totalMemory = GC.GetTotalMemory(false);
                var workingSet = Process.GetCurrentProcess().WorkingSet64;
                memoryUsage = (double)totalMemory / workingSet;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not get system load metrics");
            }
            
            return new SystemLoad { CpuUsage = cpuUsage, MemoryUsage = memoryUsage };
        }
        
        public void Dispose()
        {
            Stop();
            _concurrencySemaphore?.Dispose();
            _cpuCounter?.Dispose();
        }
        
        private class SystemLoad
        {
            public double CpuUsage { get; set; }
            public double MemoryUsage { get; set; }
        }
    }
}