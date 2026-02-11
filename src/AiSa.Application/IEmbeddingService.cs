namespace AiSa.Application;

/// <summary>
/// Embedding service interface for generating vector embeddings from text.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate a vector embedding for the given text.
    /// </summary>
    /// <param name="text">Text to generate embedding for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Vector embedding (1536 dimensions for text-embedding-ada-002).</returns>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate vector embeddings for multiple texts in batch.
    /// </summary>
    /// <param name="texts">Texts to generate embeddings for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of vector embeddings in the same order as input texts.</returns>
    Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default);
}
