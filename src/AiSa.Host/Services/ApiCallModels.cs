namespace AiSa.Host.Services;

/// <summary>
/// Represents a single API call record.
/// </summary>
public class ApiCallRecord
{
    /// <summary>
    /// Timestamp when the call was made.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// HTTP method (GET, POST, etc.).
    /// </summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Request path (without query string).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }
}

/// <summary>
/// Statistics about API calls in a session.
/// </summary>
public class ApiCallStatistics
{
    /// <summary>
    /// Total number of API calls.
    /// </summary>
    public int TotalCalls { get; set; }

    /// <summary>
    /// Number of error calls (4xx and 5xx).
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Average latency in milliseconds.
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// Timestamp of the last call, or null if no calls.
    /// </summary>
    public DateTimeOffset? LastCallTimestamp { get; set; }
}

/// <summary>
/// Event arguments for API call recorded event.
/// </summary>
public class ApiCallRecordedEventArgs : EventArgs
{
    /// <summary>
    /// The session ID for which the call was recorded.
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}
