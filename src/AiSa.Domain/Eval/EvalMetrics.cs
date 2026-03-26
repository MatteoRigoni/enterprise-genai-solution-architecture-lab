namespace AiSa.Domain.Eval;

using System.Text.Json.Serialization;

public sealed class EvalMetrics
{
    [JsonPropertyName("answeredRate")]
    public double AnsweredRate { get; init; }

    [JsonPropertyName("citationPresenceRate")]
    public double CitationPresenceRate { get; init; }

    [JsonPropertyName("citationAccuracyRate")]
    public double CitationAccuracyRate { get; init; }

    [JsonPropertyName("hallucinationRate")]
    public double HallucinationRate { get; init; }

    [JsonPropertyName("avgLatencyMs")]
    public double AvgLatencyMs { get; init; }

    [JsonPropertyName("p95LatencyMs")]
    public double P95LatencyMs { get; init; }

    [JsonPropertyName("totalQuestions")]
    public int TotalQuestions { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }
}

