using AiSa.Application.Models;
using System.Collections.Concurrent;

namespace AiSa.Application;

/// <summary>
/// In-memory implementation of document metadata store.
/// Note: Data is lost on application restart. For production, use a persistent store.
/// </summary>
public class InMemoryDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly ConcurrentDictionary<string, DocumentMetadata> _documents = new();

    public Task StoreAsync(IngestionResult result)
    {
        var metadata = new DocumentMetadata
        {
            DocumentId = result.SourceId,
            SourceName = result.SourceName,
            ChunkCount = result.ChunkCount,
            IndexedAt = result.CompletedAt,
            Status = result.Status
        };

        _documents.AddOrUpdate(
            result.SourceId,
            metadata,
            (key, oldValue) => metadata); // Update if exists

        return Task.CompletedTask;
    }

    public Task<IEnumerable<DocumentMetadata>> GetAllAsync()
    {
        var allDocuments = _documents.Values
            .OrderByDescending(d => d.IndexedAt)
            .ToList();

        return Task.FromResult<IEnumerable<DocumentMetadata>>(allDocuments);
    }

    public Task<DocumentMetadata?> GetByIdAsync(string documentId)
    {
        _documents.TryGetValue(documentId, out var metadata);
        return Task.FromResult(metadata);
    }
}
