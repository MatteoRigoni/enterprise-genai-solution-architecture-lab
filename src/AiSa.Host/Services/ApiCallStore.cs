using System.Collections.Concurrent;

namespace AiSa.Host.Services;

/// <summary>
/// Thread-safe singleton store for API call tracking.
/// Each session maintains a bounded ring buffer of recent calls.
/// </summary>
public class ApiCallStore : IApiCallStore
{
    private const int MaxRecordsPerSession = 100;
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly object _cleanupLock = new();

    /// <inheritdoc/>
    public event EventHandler<ApiCallRecordedEventArgs>? ApiCallRecorded;

    public void RecordCall(string sessionId, ApiCallRecord record)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        var sessionData = _sessions.GetOrAdd(sessionId, _ => new SessionData());
        
        lock (sessionData)
        {
            sessionData.Records.Add(record);
            sessionData.LastActivity = DateTimeOffset.UtcNow;

            if (sessionData.Records.Count > MaxRecordsPerSession)
            {
                sessionData.Records.RemoveAt(0);
            }
        }

        ApiCallRecorded?.Invoke(this, new ApiCallRecordedEventArgs { SessionId = sessionId });
    }

    public ApiCallStatistics? GetStatistics(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var sessionData))
        {
            return null;
        }

        lock (sessionData)
        {
            if (sessionData.Records.Count == 0)
            {
                return new ApiCallStatistics
                {
                    TotalCalls = 0,
                    ErrorCount = 0,
                    AverageLatencyMs = 0,
                    LastCallTimestamp = null
                };
            }

            return new ApiCallStatistics
            {
                TotalCalls = sessionData.Records.Count,
                ErrorCount = sessionData.Records.Count(r => r.StatusCode >= 400),
                AverageLatencyMs = sessionData.Records.Average(r => r.DurationMs),
                LastCallTimestamp = sessionData.Records[^1].Timestamp
            };
        }
    }

    public IReadOnlyList<ApiCallRecord> GetRecentCalls(string sessionId, int count = 5)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !_sessions.TryGetValue(sessionId, out var sessionData))
        {
            return Array.Empty<ApiCallRecord>();
        }

        lock (sessionData)
        {
            var startIndex = Math.Max(0, sessionData.Records.Count - count);
            return sessionData.Records.Skip(startIndex).Take(count).ToList();
        }
    }

    public void CleanupExpiredSessions(TimeSpan ttl)
    {
        var cutoff = DateTimeOffset.UtcNow - ttl;
        var sessionsToRemove = new List<string>();

        foreach (var kvp in _sessions)
        {
            lock (kvp.Value)
            {
                if (kvp.Value.LastActivity < cutoff)
                {
                    sessionsToRemove.Add(kvp.Key);
                }
            }
        }

        foreach (var sessionId in sessionsToRemove)
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }

    private class SessionData
    {
        public List<ApiCallRecord> Records { get; } = new();
        public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
    }
}
