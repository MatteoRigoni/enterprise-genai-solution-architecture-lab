namespace AiSa.Application;

/// <summary>
/// Retrieval service interface for querying vector store.
/// </summary>
public interface IRetrievalService
{
    /// <summary>
    /// Retrieve relevant document chunks for a query.
    /// </summary>
    /// <param name="query">User query text.</param>
    /// <param name="topK">Number of top results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results ordered by similarity score (highest first).</returns>
    Task<IEnumerable<Models.SearchResult>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default);
}
