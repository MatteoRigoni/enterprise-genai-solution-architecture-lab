using AiSa.Domain.Eval;

namespace AiSa.Application.Eval;

public sealed class EvalService : IEvalService
{
    public EvalMetrics ComputeMetrics(IReadOnlyList<EvalResult> results)
    {
        if (results is null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        if (results.Count == 0)
        {
            return new EvalMetrics
            {
                AnsweredRate = 0,
                CitationPresenceRate = 0,
                CitationAccuracyRate = 0,
                HallucinationRate = 0,
                AvgLatencyMs = 0,
                P95LatencyMs = 0,
                TotalQuestions = 0,
                Timestamp = DateTimeOffset.UtcNow
            };
        }

        var total = results.Count;
        var answeredCount = results.Count(r => r.Answered);
        var citationsPresentCount = results.Count(r => r.CitationsPresent);

        var accuracyCandidates = results.Where(r => r.CitationAccurate.HasValue).ToList();
        var citationAccurateCount = accuracyCandidates.Count(r => r.CitationAccurate == true);

        var hallucinationCandidates = results.Where(r => r.HallucinationDetected.HasValue).ToList();
        var hallucinationCount = hallucinationCandidates.Count(r => r.HallucinationDetected == true);

        var latencies = results.Select(r => r.LatencyMs).Where(l => l >= 0).OrderBy(l => l).ToArray();
        var avgLatency = latencies.Length == 0 ? 0 : latencies.Average();
        var p95Latency = latencies.Length == 0
            ? 0
            : latencies[(int)Math.Floor(0.95 * (latencies.Length - 1))];

        return new EvalMetrics
        {
            AnsweredRate = answeredCount / (double)total,
            CitationPresenceRate = citationsPresentCount / (double)total,
            CitationAccuracyRate = accuracyCandidates.Count == 0
                ? 0
                : citationAccurateCount / (double)accuracyCandidates.Count,
            HallucinationRate = hallucinationCandidates.Count == 0
                ? 0
                : hallucinationCount / (double)hallucinationCandidates.Count,
            AvgLatencyMs = avgLatency,
            P95LatencyMs = p95Latency,
            TotalQuestions = total,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}

