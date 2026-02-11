namespace AiSa.Application.Models;

/// <summary>
/// Result of document ingestion operation.
/// </summary>
public class IngestionResult
{
    /// <summary>
    /// Source document identifier.
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Source document name (e.g., filename).
    /// </summary>
    public required string SourceName { get; init; }

    /// <summary>
    /// Number of chunks created from the document.
    /// </summary>
    public required int ChunkCount { get; init; }

    /// <summary>
    /// Ingestion status.
    /// </summary>
    public required IngestionStatus Status { get; init; }

    /// <summary>
    /// Error message if ingestion failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp when ingestion completed.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// Ingestion status enumeration.
/// </summary>
public enum IngestionStatus
{
    /// <summary>
    /// Ingestion completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Ingestion failed.
    /// </summary>
    Failed
}
