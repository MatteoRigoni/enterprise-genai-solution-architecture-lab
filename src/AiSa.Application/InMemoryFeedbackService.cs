using AiSa.Application.Models;
using Microsoft.Extensions.Logging;

namespace AiSa.Application;

/// <summary>
/// In-memory feedback service implementation.
/// </summary>
public class InMemoryFeedbackService : IFeedbackService
{
    private readonly ILogger<InMemoryFeedbackService> _logger;
    private readonly Dictionary<string, List<FeedbackRequest>> _feedback = new();
    private readonly object _lock = new();

    public InMemoryFeedbackService(ILogger<InMemoryFeedbackService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task SubmitFeedbackAsync(FeedbackRequest feedback)
    {
        if (feedback == null)
            throw new ArgumentNullException(nameof(feedback));

        lock (_lock)
        {
            if (!_feedback.TryGetValue(feedback.MessageId, out var feedbackList))
            {
                feedbackList = new List<FeedbackRequest>();
                _feedback[feedback.MessageId] = feedbackList;
            }

            feedbackList.Add(feedback);

            _logger.LogInformation(
                "Feedback submitted. MessageId: {MessageId}, Rating: {Rating}, CommentLength: {CommentLength}",
                feedback.MessageId,
                feedback.Rating,
                feedback.Comment?.Length ?? 0);
        }

        return Task.CompletedTask;
    }

    public Task<FeedbackStats?> GetFeedbackStatsAsync(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return Task.FromResult<FeedbackStats?>(null);

        lock (_lock)
        {
            if (!_feedback.TryGetValue(messageId, out var feedbackList))
            {
                return Task.FromResult<FeedbackStats?>(null);
            }

            var stats = new FeedbackStats
            {
                PositiveCount = feedbackList.Count(f => f.Rating == "positive"),
                NegativeCount = feedbackList.Count(f => f.Rating == "negative")
            };

            return Task.FromResult<FeedbackStats?>(stats);
        }
    }
}
