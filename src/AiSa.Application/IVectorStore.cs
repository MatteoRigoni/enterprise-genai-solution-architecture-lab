namespace AiSa.Application;

/// <summary>
/// Provider-agnostic vector store interface (ADR-0003).
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Add or update document chunks in the vector store.
    /// </summary>
    /// <param name="chunks">Document chunks with vectors and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task AddDocumentsAsync(IEnumerable<Models.DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search for similar document chunks using vector similarity.
    /// </summary>
    /// <param name="queryVector">Query vector (embedding) to search for.</param>
    /// <param name="topK">Number of top results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results ordered by similarity score (highest first).</returns>
    Task<IEnumerable<Models.SearchResult>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all chunks associated with a source document.
    /// </summary>
    /// <param name="sourceId">Source document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the async operation.</returns>
    Task DeleteBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}
