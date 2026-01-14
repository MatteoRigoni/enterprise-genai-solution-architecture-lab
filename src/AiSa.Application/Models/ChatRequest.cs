namespace AiSa.Application.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Request DTO for chat operations.
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// User message content.
    /// </summary>
    [Required, MinLength(1)]
    public required string Message { get; init; }
}

