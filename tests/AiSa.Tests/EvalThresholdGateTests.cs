using AiSa.Application.Eval;
using AiSa.Domain.Eval;

namespace AiSa.Tests;

public class EvalThresholdGateTests
{
    [Fact]
    public void GetFailureReason_ReturnsNull_WhenNoThresholdsConfigured()
    {
        var metrics = new EvalMetrics
        {
            AnsweredRate = 0.1,
            CitationPresenceRate = 0.1,
            TotalQuestions = 10
        };
        var options = new EvalThresholdOptions();

        Assert.Null(EvalThresholdGate.GetFailureReason(metrics, options));
    }

    [Fact]
    public void GetFailureReason_ReturnsMessage_WhenAnsweredRateBelowMinimum()
    {
        var metrics = new EvalMetrics { AnsweredRate = 0.5, CitationPresenceRate = 1, TotalQuestions = 10 };
        var options = new EvalThresholdOptions { MinAnsweredRate = 0.9 };

        var reason = EvalThresholdGate.GetFailureReason(metrics, options);

        Assert.NotNull(reason);
        Assert.Contains("AnsweredRate", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void GetFailureReason_ReturnsNull_WhenMetricsMeetThresholds()
    {
        var metrics = new EvalMetrics { AnsweredRate = 0.95, CitationPresenceRate = 0.9, TotalQuestions = 10 };
        var options = new EvalThresholdOptions
        {
            MinAnsweredRate = 0.8,
            MinCitationPresenceRate = 0.8
        };

        Assert.Null(EvalThresholdGate.GetFailureReason(metrics, options));
    }

    [Fact]
    public void GetFailureReason_ChecksCitationPresence_WhenAnsweredPasses()
    {
        var metrics = new EvalMetrics { AnsweredRate = 1, CitationPresenceRate = 0.5, TotalQuestions = 10 };
        var options = new EvalThresholdOptions
        {
            MinAnsweredRate = 0.8,
            MinCitationPresenceRate = 0.8
        };

        var reason = EvalThresholdGate.GetFailureReason(metrics, options);

        Assert.NotNull(reason);
        Assert.Contains("CitationPresenceRate", reason, StringComparison.Ordinal);
    }
}
