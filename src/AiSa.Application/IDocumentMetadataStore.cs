using AiSa.Application.Models;

namespace AiSa.Application;

/// <summary>
/// Document metadata store interface for tracking ingested documents.
/// </summary>
public interface IDocumentMetadataStore
{
    /// <summary>
    /// Store document metadata after ingestion.
    /// </summary>
    /// <param name="result">Ingestion result containing metadata.</param>
    /// <returns>Task representing the async operation.</returns>
    Task StoreAsync(IngestionResult result);

    /// <summary>
    /// Get all stored document metadata.
    /// </summary>
    /// <returns>List of document metadata.</returns>
    Task<IEnumerable<DocumentMetadata>> GetAllAsync();

    /// <summary>
    /// Get document metadata by document ID.
    /// </summary>
    /// <param name="documentId">Document identifier.</param>
    /// <returns>Document metadata if found, null otherwise.</returns>
    Task<DocumentMetadata?> GetByIdAsync(string documentId);

    /// <summary>
    /// Get the latest version of a document by source name.
    /// </summary>
    /// <param name="sourceName">Source document name.</param>
    /// <returns>Latest version document metadata if found, null otherwise.</returns>
    Task<DocumentMetadata?> GetLatestBySourceNameAsync(string sourceName);

    /// <summary>
    /// Mark a document version as deprecated.
    /// </summary>
    /// <param name="documentId">Document identifier to deprecate.</param>
    /// <returns>Task representing the async operation.</returns>
    Task DeprecateVersionAsync(string documentId);
}
