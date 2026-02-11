using AiSa.Application.Models;

namespace AiSa.Application;

/// <summary>
/// Document ingestion service interface.
/// Orchestrates the full ingestion pipeline: parsing → chunking → embedding → indexing.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Ingest a text document from a stream.
    /// </summary>
    /// <param name="stream">Stream containing document content.</param>
    /// <param name="sourceId">Source document identifier.</param>
    /// <param name="sourceName">Source document name (e.g., filename).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion result with status and metadata.</returns>
    Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingest a text document from a stream, optionally updating an existing document.
    /// </summary>
    /// <param name="stream">Stream containing document content.</param>
    /// <param name="sourceId">Source document identifier.</param>
    /// <param name="sourceName">Source document name (e.g., filename).</param>
    /// <param name="updateExisting">If true, marks previous version as deprecated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion result with status and metadata.</returns>
    Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        bool updateExisting,
        CancellationToken cancellationToken = default);
}
