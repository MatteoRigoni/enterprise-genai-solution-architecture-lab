namespace AiSa.Application.Models;

/// <summary>
/// Governance inputs for document ingestion (metadata only in logs).
/// </summary>
public sealed class IngestionGovernanceContext
{
    public required DataClassification Classification { get; init; }

    public required string Owner { get; init; }

    public required string SourceType { get; init; }

    /// <summary>
    /// Version number stored on chunks for lineage (computed by caller before ingest).
    /// </summary>
    public required int DocumentVersion { get; init; }

    public bool ConfidentialApproved { get; init; }

    public string? ApprovedBy { get; init; }

    public DateTimeOffset? ApprovedAt { get; init; }

    public DateTimeOffset? LastReviewedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Fallback for programmatic ingestion without a form (lab / tests).
    /// </summary>
    public static IngestionGovernanceContext Default(int documentVersion = 1) =>
        new()
        {
            Classification = DataClassification.Internal,
            Owner = "unspecified",
            SourceType = "file",
            DocumentVersion = documentVersion,
            ConfidentialApproved = false,
            ApprovedBy = null,
            ApprovedAt = null,
            LastReviewedAt = null,
            ExpiresAt = null
        };
}
