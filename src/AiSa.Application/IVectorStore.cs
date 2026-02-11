namespace AiSa.Application;

/// <summary>
/// Provider-agnostic vector store interface (ADR-0003).
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Add or update document chunks in the vector store.
    /// </summary>
    Task AddDocumentsAsync(IEnumerable<Models.DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar document chunks using vector similarity.
    /// </summary>
    Task<IEnumerable<Models.SearchResult>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all chunks associated with a source document.
    /// </summary>
    Task DeleteBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}
