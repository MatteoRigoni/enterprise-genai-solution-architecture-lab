namespace AiSa.Application.Models;

/// <summary>
/// Search result from vector store query.
/// </summary>
public class SearchResult
{
    /// <summary>
    /// Document chunk that matched the query.
    /// </summary>
    public required DocumentChunk Chunk { get; init; }

    /// <summary>
    /// Similarity score (higher is more similar, typically 0-1 range for cosine similarity).
    /// </summary>
    public required double Score { get; init; }
}
