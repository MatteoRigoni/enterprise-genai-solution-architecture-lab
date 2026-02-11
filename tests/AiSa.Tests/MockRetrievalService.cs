using AiSa.Application;
using AiSa.Application.Models;

namespace AiSa.Tests;

/// <summary>
/// Mock retrieval service for integration tests.
/// Returns empty results by default to simulate "no documents" scenario.
/// </summary>
public class MockRetrievalService : IRetrievalService
{
    public Task<IEnumerable<SearchResult>> RetrieveAsync(string query, int topK, CancellationToken cancellationToken = default)
    {
        // Return empty results to simulate "no documents indexed" scenario
        // This makes ChatService return "I don't know based on provided documents."
        return Task.FromResult(Enumerable.Empty<SearchResult>());
    }
}
