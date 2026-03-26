namespace AiSa.Domain.Eval;

using System.Text.Json.Serialization;

public sealed class EvalResult
{
    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("actualResponse")]
    public string ActualResponse { get; init; } = string.Empty;

    [JsonPropertyName("answered")]
    public bool Answered { get; init; }

    [JsonPropertyName("citationsPresent")]
    public bool CitationsPresent { get; init; }

    [JsonPropertyName("citationAccurate")]
    public bool? CitationAccurate { get; init; }

    [JsonPropertyName("hallucinationDetected")]
    public bool? HallucinationDetected { get; init; }

    [JsonPropertyName("latencyMs")]
    public long LatencyMs { get; init; }
}

