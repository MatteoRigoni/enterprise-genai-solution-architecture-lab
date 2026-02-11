using AiSa.Application.Models;

namespace AiSa.Application;

/// <summary>
/// Feedback service interface for collecting user feedback on chat responses.
/// </summary>
public interface IFeedbackService
{
    /// <summary>
    /// Submits feedback for a chat message.
    /// </summary>
    /// <param name="feedback">Feedback data.</param>
    /// <returns>Task representing the async operation.</returns>
    Task SubmitFeedbackAsync(FeedbackRequest feedback);

    /// <summary>
    /// Gets feedback statistics for a message.
    /// </summary>
    /// <param name="messageId">Message identifier.</param>
    /// <returns>Feedback statistics if available.</returns>
    Task<FeedbackStats?> GetFeedbackStatsAsync(string messageId);
}

/// <summary>
/// Feedback statistics for a message.
/// </summary>
public class FeedbackStats
{
    /// <summary>
    /// Number of positive feedbacks.
    /// </summary>
    public int PositiveCount { get; set; }

    /// <summary>
    /// Number of negative feedbacks.
    /// </summary>
    public int NegativeCount { get; set; }

    /// <summary>
    /// Total feedback count.
    /// </summary>
    public int TotalCount => PositiveCount + NegativeCount;
}
