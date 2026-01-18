using AiSa.Application;
using AiSa.Application.Models;

namespace AiSa.Tests;

/// <summary>
/// Mock retrieval service for integration tests that returns predefined results.
/// Used to test RAG flow when documents are retrieved successfully.
/// </summary>
public class MockRetrievalServiceWithResults : IRetrievalService
{
    public Task<IEnumerable<SearchResult>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        // Return mock search results to simulate successful retrieval
        var results = new List<SearchResult>
        {
            new SearchResult
            {
                Chunk = new DocumentChunk
                {
                    ChunkId = "chunk-1",
                    SourceId = "doc-1",
                    SourceName = "faq.txt",
                    Content = "Artificial Intelligence (AI) is the simulation of human intelligence by machines.",
                    ChunkIndex = 0,
                    Vector = new float[1536],
                    IndexedAt = DateTimeOffset.UtcNow
                },
                Score = 0.95f
            },
            new SearchResult
            {
                Chunk = new DocumentChunk
                {
                    ChunkId = "chunk-2",
                    SourceId = "doc-1",
                    SourceName = "faq.txt",
                    Content = "Machine learning is a subset of AI that enables systems to learn from data.",
                    ChunkIndex = 1,
                    Vector = new float[1536],
                    IndexedAt = DateTimeOffset.UtcNow
                },
                Score = 0.85f
            }
        };

        return Task.FromResult<IEnumerable<SearchResult>>(results);
    }
}
