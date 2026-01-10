namespace AiSa.Application.Models;

/// <summary>
/// Request DTO for chat operations.
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User message content.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Optional correlation ID. If not provided, will be generated server-side.
    /// </summary>
    public string? CorrelationId { get; init; }
}

