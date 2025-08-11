using Crawlee.NET.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Crawlee.NET.SessionPool
{
    public class SessionPoolOptions
    {
        public int MaxPoolSize { get; set; } = 1000;
        public int SessionTtlSeconds { get; set; } = 1800; // 30 minutes
        public bool PersistStateKeyValueStoreId { get; set; } = true;
        public string? ProxyConfiguration { get; set; }
        public bool CreateSessionFunction { get; set; } = true;
        public bool ValidateProxyFunction { get; set; } = true;
    }

    public class SessionPool : IDisposable
    {
        private readonly SessionPoolOptions _options;
        private readonly ILogger<SessionPool>? _logger;
        private readonly ConcurrentDictionary<string, Session> _sessions = new();
        private readonly ConcurrentQueue<string> _availableSessionIds = new();
        private readonly Timer _cleanupTimer;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        
        public int SessionCount => _sessions.Count;
        public int AvailableSessionCount => _availableSessionIds.Count;
        
        public SessionPool(SessionPoolOptions? options = null, ILogger<SessionPool>? logger = null)
        {
            _options = options ?? new SessionPoolOptions();
            _logger = logger;
            
            // Cleanup expired sessions every 5 minutes
            _cleanupTimer = new Timer(CleanupExpiredSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
        
        public async Task<Session> GetSession()
        {
            await _semaphore.WaitAsync();
            try
            {
                // Try to get an available session
                if (_availableSessionIds.TryDequeue(out var sessionId) && _sessions.TryGetValue(sessionId, out var session))
                {
                    if (!session.IsBlocked && !IsSessionExpired(session))
                    {
                        session.LastUsed = DateTime.UtcNow;
                        return session;
                    }
                    else
                    {
                        // Remove expired or blocked session
                        _sessions.TryRemove(sessionId, out _);
                    }
                }
                
                // Create new session if under limit
                if (_sessions.Count < _options.MaxPoolSize)
                {
                    var newSession = CreateNewSession();
                    _sessions.TryAdd(newSession.Id, newSession);
                    return newSession;
                }
                
                // If at limit, find least recently used session
                var lruSession = _sessions.Values
                    .Where(s => !s.IsBlocked)
                    .OrderBy(s => s.LastUsed)
                    .FirstOrDefault();
                
                if (lruSession != null)
                {
                    lruSession.LastUsed = DateTime.UtcNow;
                    return lruSession;
                }
                
                // Fallback: create new session anyway
                var fallbackSession = CreateNewSession();
                _sessions.TryAdd(fallbackSession.Id, fallbackSession);
                return fallbackSession;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task ReturnSession(Session session)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_sessions.ContainsKey(session.Id) && !session.IsBlocked && !IsSessionExpired(session))
                {
                    _availableSessionIds.Enqueue(session.Id);
                }
                else if (session.IsBlocked)
                {
                    _sessions.TryRemove(session.Id, out _);
                    _logger?.LogDebug("Removed blocked session: {SessionId}", session.Id);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public async Task MarkSessionBad(Session session, string? reason = null)
        {
            session.MarkErrored();
            _logger?.LogWarning("Session marked as bad: {SessionId}, Reason: {Reason}, ErrorCount: {ErrorCount}", 
                session.Id, reason, session.ErrorCount);
            
            if (session.IsBlocked)
            {
                await _semaphore.WaitAsync();
                try
                {
                    _sessions.TryRemove(session.Id, out _);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }
        
        public async Task MarkSessionGood(Session session)
        {
            session.MarkSuccessful();
            _logger?.LogDebug("Session marked as good: {SessionId}, ErrorCount: {ErrorCount}", 
                session.Id, session.ErrorCount);
            await Task.CompletedTask;
        }
        
        private Session CreateNewSession()
        {
            var session = new Session
            {
                CreatedAt = DateTime.UtcNow,
                LastUsed = DateTime.UtcNow
            };
            
            // Add default headers
            session.Headers["User-Agent"] = "Crawlee.NET/1.0";
            session.Headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            session.Headers["Accept-Language"] = "en-US,en;q=0.5";
            session.Headers["Accept-Encoding"] = "gzip, deflate";
            session.Headers["Connection"] = "keep-alive";
            
            _logger?.LogDebug("Created new session: {SessionId}", session.Id);
            return session;
        }
        
        private bool IsSessionExpired(Session session)
        {
            return DateTime.UtcNow - session.CreatedAt > TimeSpan.FromSeconds(_options.SessionTtlSeconds);
        }
        
        private void CleanupExpiredSessions(object? state)
        {
            _semaphore.Wait();
            try
            {
                var expiredSessions = _sessions.Values
                    .Where(IsSessionExpired)
                    .ToList();
                
                foreach (var session in expiredSessions)
                {
                    _sessions.TryRemove(session.Id, out _);
                }
                
                if (expiredSessions.Count > 0)
                {
                    _logger?.LogDebug("Cleaned up {Count} expired sessions", expiredSessions.Count);
                }
                
                // Clean up available session queue
                var validSessionIds = new List<string>();
                while (_availableSessionIds.TryDequeue(out var sessionId))
                {
                    if (_sessions.ContainsKey(sessionId))
                    {
                        validSessionIds.Add(sessionId);
                    }
                }
                
                foreach (var sessionId in validSessionIds)
                {
                    _availableSessionIds.Enqueue(sessionId);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _semaphore?.Dispose();
        }
    }
}