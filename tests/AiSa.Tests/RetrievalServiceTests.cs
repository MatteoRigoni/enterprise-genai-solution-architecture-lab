using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;

namespace AiSa.Tests;

/// <summary>
/// Unit tests for RetrievalService (T02.D).
/// Tests retrieval logic: query embedding, vector search, empty results handling.
/// </summary>
public class RetrievalServiceTests
{
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IVectorStore> _mockVectorStore;
    private readonly Mock<ILogger<RetrievalService>> _mockLogger;
    private readonly ActivitySource _activitySource;
    private readonly RetrievalService _retrievalService;

    public RetrievalServiceTests()
    {
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockVectorStore = new Mock<IVectorStore>();
        _mockLogger = new Mock<ILogger<RetrievalService>>();
        _activitySource = new ActivitySource("AiSa.Tests");

        _retrievalService = new RetrievalService(
            _mockEmbeddingService.Object,
            _mockVectorStore.Object,
            _activitySource,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RetrieveAsync_WithValidQuery_ReturnsSearchResults()
    {
        // Arrange
        var query = "What is AI?";
        var topK = 3;
        var queryEmbedding = new float[1536]; // 1536 dimensions for text-embedding-ada-002
        Array.Fill(queryEmbedding, 0.1f);

        var expectedResults = new List<SearchResult>
        {
            new SearchResult
            {
                Chunk = new DocumentChunk
                {
                    ChunkId = "chunk-1",
                    SourceId = "doc-1",
                    SourceName = "test.txt",
                    Content = "AI is artificial intelligence.",
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
                    SourceName = "test.txt",
                    Content = "Machine learning is a subset of AI.",
                    ChunkIndex = 1,
                    Vector = new float[1536],
                    IndexedAt = DateTimeOffset.UtcNow
                },
                Score = 0.85f
            }
        };

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockVectorStore
            .Setup(v => v.SearchAsync(queryEmbedding, topK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _retrievalService.RetrieveAsync(query, topK);

        // Assert
        Assert.NotNull(result);
        var resultsList = result.ToList();
        Assert.Equal(2, resultsList.Count);
        Assert.Equal(0.95f, resultsList[0].Score); // Results should be ordered by score (descending)
        Assert.Equal(0.85f, resultsList[1].Score);

        // Verify chunk metadata
        Assert.Equal("chunk-1", resultsList[0].Chunk.ChunkId);
        Assert.Equal("doc-1", resultsList[0].Chunk.SourceId);
        Assert.Equal("test.txt", resultsList[0].Chunk.SourceName);

        // Verify service calls
        _mockEmbeddingService.Verify(e => e.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _mockVectorStore.Verify(v => v.SearchAsync(queryEmbedding, topK, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_WithEmptyQuery_ThrowsArgumentException()
    {
        // Arrange
        var topK = 3;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _retrievalService.RetrieveAsync(string.Empty, topK));
    }

    [Fact]
    public async Task RetrieveAsync_WithWhitespaceQuery_ThrowsArgumentException()
    {
        // Arrange
        var topK = 3;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _retrievalService.RetrieveAsync("   ", topK));
    }

    [Fact]
    public async Task RetrieveAsync_WithZeroTopK_ThrowsArgumentException()
    {
        // Arrange
        var query = "test query";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _retrievalService.RetrieveAsync(query, 0));
    }

    [Fact]
    public async Task RetrieveAsync_WithNegativeTopK_ThrowsArgumentException()
    {
        // Arrange
        var query = "test query";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _retrievalService.RetrieveAsync(query, -1));
    }

    [Fact]
    public async Task RetrieveAsync_WithNoResults_ReturnsEmptyList()
    {
        // Arrange
        var query = "query with no matches";
        var topK = 3;
        var queryEmbedding = new float[1536];
        Array.Fill(queryEmbedding, 0.1f);

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockVectorStore
            .Setup(v => v.SearchAsync(queryEmbedding, topK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<SearchResult>());

        // Act
        var result = await _retrievalService.RetrieveAsync(query, topK);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);

        // Verify service calls were made
        _mockEmbeddingService.Verify(e => e.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()), Times.Once);
        _mockVectorStore.Verify(v => v.SearchAsync(queryEmbedding, topK, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_WithMultipleResults_ReturnsResultsOrderedByScore()
    {
        // Arrange
        var query = "test query";
        var topK = 5;
        var queryEmbedding = new float[1536];
        Array.Fill(queryEmbedding, 0.1f);

        var expectedResults = new List<SearchResult>
        {
            new SearchResult
            {
                Chunk = new DocumentChunk { ChunkId = "chunk-1", SourceId = "doc-1", SourceName = "test.txt", Content = "Content 1", ChunkIndex = 0, Vector = new float[1536], IndexedAt = DateTimeOffset.UtcNow },
                Score = 0.70f
            },
            new SearchResult
            {
                Chunk = new DocumentChunk { ChunkId = "chunk-2", SourceId = "doc-1", SourceName = "test.txt", Content = "Content 2", ChunkIndex = 1, Vector = new float[1536], IndexedAt = DateTimeOffset.UtcNow },
                Score = 0.95f
            },
            new SearchResult
            {
                Chunk = new DocumentChunk { ChunkId = "chunk-3", SourceId = "doc-1", SourceName = "test.txt", Content = "Content 3", ChunkIndex = 2, Vector = new float[1536], IndexedAt = DateTimeOffset.UtcNow },
                Score = 0.80f
            }
        };

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        _mockVectorStore
            .Setup(v => v.SearchAsync(queryEmbedding, topK, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var result = await _retrievalService.RetrieveAsync(query, topK);

        // Assert
        Assert.NotNull(result);
        var resultsList = result.ToList();
        Assert.Equal(3, resultsList.Count);

        // Verify all expected results are present (VectorStore is responsible for ordering)
        var scores = resultsList.Select(r => r.Score).ToList();
        Assert.Contains(0.70f, scores);
        Assert.Contains(0.95f, scores);
        Assert.Contains(0.80f, scores);
        
        // Verify chunk IDs are present
        var chunkIds = resultsList.Select(r => r.Chunk.ChunkId).ToList();
        Assert.Contains("chunk-1", chunkIds);
        Assert.Contains("chunk-2", chunkIds);
        Assert.Contains("chunk-3", chunkIds);
    }

    [Fact]
    public async Task RetrieveAsync_WhenEmbeddingServiceThrows_PropagatesException()
    {
        // Arrange
        var query = "test query";
        var topK = 3;

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _retrievalService.RetrieveAsync(query, topK));

        Assert.Equal("Embedding service error", exception.Message);
        _mockVectorStore.Verify(v => v.SearchAsync(It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
