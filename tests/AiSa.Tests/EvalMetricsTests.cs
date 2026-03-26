using AiSa.Application.Eval;
using AiSa.Domain.Eval;

namespace AiSa.Tests;

public class EvalMetricsTests
{
    [Fact]
    public void ComputeMetrics_ComputesExpectedRates_AndLatency()
    {
        var service = new EvalService();

        var results = new[]
        {
            new EvalResult
            {
                Question = "Q1",
                ActualResponse = "A1",
                Answered = true,
                CitationsPresent = true,
                CitationAccurate = true,
                HallucinationDetected = false,
                LatencyMs = 1000
            },
            new EvalResult
            {
                Question = "Q2",
                ActualResponse = "A2",
                Answered = true,
                CitationsPresent = false,
                CitationAccurate = null,
                HallucinationDetected = true,
                LatencyMs = 2000
            },
            new EvalResult
            {
                Question = "Q3",
                ActualResponse = "A3",
                Answered = false,
                CitationsPresent = true,
                CitationAccurate = false,
                HallucinationDetected = null,
                LatencyMs = 3000
            }
        };

        var metrics = service.ComputeMetrics(results);

        Assert.Equal(3, metrics.TotalQuestions);

        // answered: 2/3
        Assert.Equal(2d / 3d, metrics.AnsweredRate, 3);

        // citations present: 2/3
        Assert.Equal(2d / 3d, metrics.CitationPresenceRate, 3);

        // citation accuracy: 1 accurate out of 2 candidates
        Assert.Equal(0.5d, metrics.CitationAccuracyRate, 3);

        // hallucination rate: 1 hallucination out of 2 candidates
        Assert.Equal(0.5d, metrics.HallucinationRate, 3);

        // latency: avg and p95
        Assert.Equal(2000d, metrics.AvgLatencyMs, 3);
        Assert.True(metrics.P95LatencyMs >= 2000 && metrics.P95LatencyMs <= 3000);
    }
}

