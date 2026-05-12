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
    /// Normalized source document name used as stable key for versioning.
    /// </summary>
    public string? SourceNameNormalized { get; init; }

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

    /// <summary>
    /// Document version number (starts at 1, increments for updates).
    /// </summary>
    public int Version { get; init; } = 1;

    /// <summary>
    /// Previous version document ID if this is an update.
    /// </summary>
    public string? PreviousVersionId { get; init; }

    /// <summary>
    /// Whether this version is deprecated (superseded by a newer version).
    /// </summary>
    public bool IsDeprecated { get; init; } = false;

    /// <summary>
    /// SHA-256 hash of document content.
    /// </summary>
    public string? ContentHash { get; init; }

    /// <summary>
    /// Data classification at ingestion time.
    /// </summary>
    public DataClassification Classification { get; init; } = DataClassification.Internal;

    /// <summary>
    /// Owning team or party (stored as provided; avoid logging as PII in telemetry).
    /// </summary>
    public string Owner { get; init; } = string.Empty;

    /// <summary>
    /// Source type: file, url, manual, etc.
    /// </summary>
    public string SourceType { get; init; } = "file";

    public bool ConfidentialApproved { get; init; }

    public string? ApprovedBy { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    public DateTimeOffset? LastReviewedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}
