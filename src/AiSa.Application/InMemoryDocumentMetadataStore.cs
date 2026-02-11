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
        // Check if this is an update to an existing document
        var existingLatest = _documents.Values
            .Where(d => d.SourceName == result.SourceName && !d.IsDeprecated)
            .OrderByDescending(d => d.Version)
            .FirstOrDefault();

        var version = 1;
        string? previousVersionId = null;

        if (existingLatest != null)
        {
            // This is a new version
            version = existingLatest.Version + 1;
            previousVersionId = existingLatest.DocumentId;
            
            // Deprecate previous version
            if (_documents.TryGetValue(existingLatest.DocumentId, out var prevMetadata))
            {
                var deprecatedMetadata = new DocumentMetadata
                {
                    DocumentId = prevMetadata.DocumentId,
                    SourceName = prevMetadata.SourceName,
                    ChunkCount = prevMetadata.ChunkCount,
                    IndexedAt = prevMetadata.IndexedAt,
                    Status = prevMetadata.Status,
                    Version = prevMetadata.Version,
                    PreviousVersionId = prevMetadata.PreviousVersionId,
                    IsDeprecated = true
                };
                _documents[existingLatest.DocumentId] = deprecatedMetadata;
            }
        }

        var metadata = new DocumentMetadata
        {
            DocumentId = result.SourceId,
            SourceName = result.SourceName,
            ChunkCount = result.ChunkCount,
            IndexedAt = result.CompletedAt,
            Status = result.Status,
            Version = version,
            PreviousVersionId = previousVersionId,
            IsDeprecated = false
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
            .Where(d => !d.IsDeprecated)
            .OrderByDescending(d => d.IndexedAt)
            .ToList();

        return Task.FromResult<IEnumerable<DocumentMetadata>>(allDocuments);
    }

    public Task<DocumentMetadata?> GetByIdAsync(string documentId)
    {
        _documents.TryGetValue(documentId, out var metadata);
        return Task.FromResult(metadata);
    }

    public Task<DocumentMetadata?> GetLatestBySourceNameAsync(string sourceName)
    {
        var latest = _documents.Values
            .Where(d => d.SourceName == sourceName && !d.IsDeprecated)
            .OrderByDescending(d => d.Version)
            .FirstOrDefault();

        return Task.FromResult(latest);
    }

    public Task DeprecateVersionAsync(string documentId)
    {
        if (_documents.TryGetValue(documentId, out var metadata))
        {
            var deprecatedMetadata = new DocumentMetadata
            {
                DocumentId = metadata.DocumentId,
                SourceName = metadata.SourceName,
                ChunkCount = metadata.ChunkCount,
                IndexedAt = metadata.IndexedAt,
                Status = metadata.Status,
                Version = metadata.Version,
                PreviousVersionId = metadata.PreviousVersionId,
                IsDeprecated = true
            };
            _documents[documentId] = deprecatedMetadata;
        }

        return Task.CompletedTask;
    }
}
