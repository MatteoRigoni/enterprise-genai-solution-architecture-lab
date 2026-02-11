namespace AiSa.Application.Models;

/// <summary>
/// Document chunk with vector embedding and metadata.
/// </summary>
public class DocumentChunk
{
    public required string ChunkId { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Content { get; init; }
    public required float[] Vector { get; init; }
    public required string SourceId { get; init; }
    public required string SourceName { get; init; }
    public required DateTimeOffset IndexedAt { get; init; }
}
