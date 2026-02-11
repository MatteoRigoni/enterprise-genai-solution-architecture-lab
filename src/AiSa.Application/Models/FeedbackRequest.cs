namespace AiSa.Application.Models;

/// <summary>
/// Request DTO for submitting feedback.
/// </summary>
public class FeedbackRequest
{
    /// <summary>
    /// Message identifier to provide feedback for.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Feedback rating: "positive" for thumbs up, "negative" for thumbs down.
    /// </summary>
    public required string Rating { get; init; }

    /// <summary>
    /// Optional comment explaining the feedback.
    /// </summary>
    public string? Comment { get; init; }
}
