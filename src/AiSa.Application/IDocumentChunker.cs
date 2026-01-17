namespace AiSa.Application;

/// <summary>
/// Document chunking service interface.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Chunk a document into smaller pieces with overlap.
    /// </summary>
    /// <param name="content">Document content to chunk.</param>
    /// <param name="sourceId">Source document identifier.</param>
    /// <param name="sourceName">Source document name (e.g., filename).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of document chunks with metadata.</returns>
    Task<IEnumerable<Models.DocumentChunk>> ChunkAsync(
        string content,
        string sourceId,
        string sourceName,
        CancellationToken cancellationToken = default);
}
