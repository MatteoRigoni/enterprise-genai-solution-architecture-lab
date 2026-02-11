using AiSa.Application;
using AiSa.Application.Models;

namespace AiSa.Infrastructure;

/// <summary>
/// Stub for Azure AI Search (T02). Throws if used; replace with real implementation when T02 is applied.
/// </summary>
public class AzureSearchVectorStore : IVectorStore
{
    public Task AddDocumentsAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("AzureSearch vector store is not implemented in this branch. Set VectorStore:Provider to PgVector or add T02 implementation.");

    public Task<IEnumerable<SearchResult>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("AzureSearch vector store is not implemented in this branch. Set VectorStore:Provider to PgVector or add T02 implementation.");

    public Task DeleteBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("AzureSearch vector store is not implemented in this branch. Set VectorStore:Provider to PgVector or add T02 implementation.");
}
