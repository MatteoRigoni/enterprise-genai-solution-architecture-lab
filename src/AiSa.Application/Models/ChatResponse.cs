namespace AiSa.Application.Models;

/// <summary>
/// Response DTO for chat operations.
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// Assistant response content.
    /// </summary>
    public required string Response { get; init; }

    /// <summary>
    /// Correlation ID for tracing and observability.
    /// </summary>
    public required string CorrelationId { get; init; }
}

