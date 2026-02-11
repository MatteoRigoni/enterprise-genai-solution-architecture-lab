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
    /// Similarity score. Range depends on metric:
    /// - cosine: typically 0-1 (or -1 to 1 for unnormalized), higher = more similar
    /// - dotProduct: unbounded, higher = more similar (equivalent to cosine for normalized vectors)
    /// - euclidean: distance (lower = more similar), typically positive values
    /// </summary>
    public required double Score { get; init; }
}
