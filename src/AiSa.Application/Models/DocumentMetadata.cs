namespace AiSa.Application.Models;

/// <summary>
/// Document metadata for listing ingested documents.
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Document identifier (sourceId).
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Source document name (e.g., filename).
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Number of chunks created from the document.
    /// </summary>
    public required int ChunkCount { get; init; }

    /// <summary>
    /// Timestamp when document was indexed.
    /// </summary>
    public required DateTimeOffset IndexedAt { get; init; }

    /// <summary>
    /// Ingestion status.
    /// </summary>
    public required IngestionStatus Status { get; init; }
}
