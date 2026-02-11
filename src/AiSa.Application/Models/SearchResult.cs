namespace AiSa.Application.Models;

/// <summary>
/// Search result with chunk and similarity score.
/// </summary>
public class SearchResult
{
    public required DocumentChunk Chunk { get; init; }
    public required double Score { get; init; }
}
