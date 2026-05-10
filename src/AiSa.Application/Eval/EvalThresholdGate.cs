using AiSa.Domain.Eval;

namespace AiSa.Application.Eval;

public static class EvalThresholdGate
{
    /// <summary>
    /// Returns a human-readable failure reason if any configured threshold is not met; otherwise null.
    /// </summary>
    public static string? GetFailureReason(EvalMetrics metrics, EvalThresholdOptions options)
    {
        if (options.MinAnsweredRate is { } minAnswered && metrics.AnsweredRate < minAnswered)
        {
            return $"AnsweredRate {metrics.AnsweredRate:P1} is below required {minAnswered:P1}.";
        }

        if (options.MinCitationPresenceRate is { } minCitations && metrics.CitationPresenceRate < minCitations)
        {
            return $"CitationPresenceRate {metrics.CitationPresenceRate:P1} is below required {minCitations:P1}.";
        }

        return null;
    }
}
