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

    /// <summary>
    /// Message identifier for feedback correlation.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Citations referencing document chunks used to generate the response.
    /// </summary>
    public IReadOnlyList<Citation> Citations { get; init; } = Array.Empty<Citation>();
}

