namespace AiSa.Application.Models;

/// <summary>
/// Citation reference to a document chunk.
/// </summary>
public class Citation
{
    /// <summary>
    /// Source document name (e.g., filename).
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Chunk identifier.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Similarity score for this citation (higher is more relevant).
    /// </summary>
    public double Score { get; init; }
}
