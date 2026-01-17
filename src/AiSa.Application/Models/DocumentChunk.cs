namespace AiSa.Application.Models;

/// <summary>
/// Document chunk with vector embedding and metadata.
/// </summary>
public class DocumentChunk
{
    /// <summary>
    /// Unique identifier for this chunk.
    /// </summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Position/index of this chunk within the source document (0-based).
    /// </summary>
    public required int ChunkIndex { get; init; }

    /// <summary>
    /// Text content of the chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Vector embedding for the chunk content (1536 dimensions for text-embedding-ada-002).
    /// </summary>
    public required float[] Vector { get; init; }

    /// <summary>
    /// Source document identifier.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Source document name (e.g., filename).
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Timestamp when this chunk was indexed.
    /// </summary>
    public required DateTimeOffset IndexedAt { get; init; }
}
