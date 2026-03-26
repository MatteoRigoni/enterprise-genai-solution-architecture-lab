namespace AiSa.Domain.Eval;

using System.Text.Json.Serialization;

public sealed class EvalDataset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("questions")]
    public IReadOnlyList<EvalQuestion> Questions { get; init; } = Array.Empty<EvalQuestion>();
}

