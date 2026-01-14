using System.Collections.Concurrent;

namespace AiSa.Host.Services;

/// <summary>
/// Thread-safe singleton store for API call tracking, keyed by UI session ID.
/// Each session has a bounded ring buffer of recent calls with automatic TTL cleanup.
/// </summary>
public interface IApiCallStore
{
    /// <summary>
    /// Event raised when a new API call is recorded for a session.
    /// </summary>
    event EventHandler<ApiCallRecordedEventArgs>? ApiCallRecorded;

    /// <summary>
    /// Records an API call for the specified session.
    /// </summary>
    void RecordCall(string sessionId, ApiCallRecord record);

    /// <summary>
    /// Gets statistics for a specific session.
    /// </summary>
    ApiCallStatistics? GetStatistics(string sessionId);

    /// <summary>
    /// Gets recent API calls for a specific session (up to the specified count).
    /// </summary>
    IReadOnlyList<ApiCallRecord> GetRecentCalls(string sessionId, int count = 5);

    /// <summary>
    /// Cleans up expired sessions and old records (should be called periodically).
    /// </summary>
    void CleanupExpiredSessions(TimeSpan ttl);
}
