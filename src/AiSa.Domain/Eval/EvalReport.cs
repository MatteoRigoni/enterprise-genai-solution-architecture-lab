namespace AiSa.Domain.Eval;

using System.Text.Json.Serialization;

public sealed class EvalReport
{
    [JsonPropertyName("datasetName")]
    public string DatasetName { get; init; } = string.Empty;

    [JsonPropertyName("datasetVersion")]
    public string DatasetVersion { get; init; } = string.Empty;

    [JsonPropertyName("metrics")]
    public EvalMetrics Metrics { get; init; } = new();

    [JsonPropertyName("results")]
    public IReadOnlyList<EvalResult> Results { get; init; } = Array.Empty<EvalResult>();

    [JsonPropertyName("runTimestamp")]
    public DateTimeOffset RunTimestamp { get; init; }

    [JsonPropertyName("runDurationMs")]
    public long RunDurationMs { get; init; }
}

