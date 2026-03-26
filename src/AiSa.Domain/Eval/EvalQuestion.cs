namespace AiSa.Domain.Eval;

using System.Text.Json.Serialization;

public sealed class EvalQuestion
{
    [JsonPropertyName("question")]
    public string Question { get; init; } = string.Empty;

    [JsonPropertyName("expectedKeyFacts")]
    public IReadOnlyList<string> ExpectedKeyFacts { get; init; } = Array.Empty<string>();

    [JsonPropertyName("expectedDocIds")]
    public IReadOnlyList<string>? ExpectedDocIds { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }
}

