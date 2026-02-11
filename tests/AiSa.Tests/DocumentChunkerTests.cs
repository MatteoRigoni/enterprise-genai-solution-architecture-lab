using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics;

namespace AiSa.Tests;

/// <summary>
/// Unit tests for DocumentChunker (T02.B).
/// Tests chunking logic: correct size with overlap, structure preservation, empty/short documents.
/// </summary>
public class DocumentChunkerTests
{
    private readonly Mock<ITokenCounter> _mockTokenCounter;
    private readonly Mock<ILogger<DocumentChunker>> _mockLogger;
    private readonly ChunkingOptions _options;
    private readonly DocumentChunker _chunker;

    public DocumentChunkerTests()
    {
        _mockTokenCounter = new Mock<ITokenCounter>();
        _mockLogger = new Mock<ILogger<DocumentChunker>>();
        _options = new ChunkingOptions
        {
            ChunkSizeTokens = 100,
            OverlapTokens = 20,
            MinChunkTokens = 10
        };

        // Default mock setup: estimate 4 chars per token (basic estimation)
        _mockTokenCounter.Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns<string>(text => string.IsNullOrEmpty(text) ? 0 : text.Length / 4);
        _mockTokenCounter.Setup(t => t.ModelName)
            .Returns("test-model");

        var optionsWrapper = Options.Create(_options);
        _chunker = new DocumentChunker(optionsWrapper, _mockTokenCounter.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ChunkAsync_WithEmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var sourceId = "test-doc-1";
        var sourceName = "test.txt";

        // Act
        var result = await _chunker.ChunkAsync(string.Empty, sourceId, sourceName);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ChunkAsync_WithShortContent_ReturnsSingleChunk()
    {
        // Arrange
        var content = "Short text that fits in one chunk.";
        var sourceId = "test-doc-1";
        var sourceName = "test.txt";
        
        // Content is ~35 chars, estimated ~8 tokens, which is less than ChunkSizeTokens (100)
        _mockTokenCounter.Setup(t => t.CountTokens(content))
            .Returns(8);

        // Act
        var result = await _chunker.ChunkAsync(content, sourceId, sourceName);

        // Assert
        Assert.NotNull(result);
        var chunks = result.ToList();
        Assert.Single(chunks);
        Assert.Equal(content, chunks[0].Content);
        Assert.Equal(sourceId, chunks[0].SourceId);
        Assert.Equal(sourceName, chunks[0].SourceName);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.NotNull(chunks[0].ChunkId);
    }

    [Fact]
    public async Task ChunkAsync_WithLargeContent_SplitsIntoMultipleChunks()
    {
        // Arrange
        // Create content that will be split into multiple chunks
        // Each paragraph is ~50 chars (~12 tokens), so ~500 chars (~125 tokens) will need ~2 chunks
        var paragraphs = Enumerable.Range(1, 10)
            .Select(i => $"This is paragraph {i}. It contains some text that makes it long enough.")
            .ToList();
        var content = string.Join("\n\n", paragraphs);
        var sourceId = "test-doc-1";
        var sourceName = "test.txt";

        // Set up token counting: total content ~550 chars = ~137 tokens (will need 2 chunks with size 100)
        _mockTokenCounter.Setup(t => t.CountTokens(content))
            .Returns(137);
        
        // For individual paragraphs, estimate tokens
        _mockTokenCounter.Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns<string>(text =>
            {
                if (string.IsNullOrEmpty(text)) return 0;
                // Estimate 4 chars per token
                return text.Length / 4;
            });

        // Act
        var result = await _chunker.ChunkAsync(content, sourceId, sourceName);

        // Assert
        Assert.NotNull(result);
        var chunks = result.ToList();
        Assert.True(chunks.Count >= 2, $"Expected at least 2 chunks, got {chunks.Count}");
        
        // Verify all chunks have correct metadata
        foreach (var chunk in chunks)
        {
            Assert.Equal(sourceId, chunk.SourceId);
            Assert.Equal(sourceName, chunk.SourceName);
            Assert.NotNull(chunk.ChunkId);
            Assert.NotEmpty(chunk.Content);
        }

        // Verify chunk indices are sequential
        var indices = chunks.Select(c => c.ChunkIndex).OrderBy(i => i).ToList();
        for (int i = 0; i < indices.Count; i++)
        {
            Assert.Equal(i, indices[i]);
        }
    }

    [Fact]
    public async Task ChunkAsync_WithParagraphs_PreservesStructure()
    {
        // Arrange
        var paragraph1 = "First paragraph with some content.";
        var paragraph2 = "Second paragraph with different content.";
        var paragraph3 = "Third paragraph with yet more content.";
        var content = string.Join("\n\n", paragraph1, paragraph2, paragraph3);
        var sourceId = "test-doc-1";
        var sourceName = "test.txt";

        // Content fits in single chunk (total ~110 chars = ~27 tokens)
        _mockTokenCounter.Setup(t => t.CountTokens(It.IsAny<string>()))
            .Returns<string>(text => string.IsNullOrEmpty(text) ? 0 : text.Length / 4);

        // Act
        var result = await _chunker.ChunkAsync(content, sourceId, sourceName);

        // Assert
        Assert.NotNull(result);
        var chunks = result.ToList();
        Assert.Single(chunks);
        
        // Content should contain paragraph separators
        Assert.Contains("\n\n", chunks[0].Content);
        Assert.Contains(paragraph1, chunks[0].Content);
        Assert.Contains(paragraph2, chunks[0].Content);
    }

    [Fact]
    public async Task ChunkAsync_WithInvalidSourceId_ThrowsArgumentException()
    {
        // Arrange
        var content = "Some content";
        var sourceName = "test.txt";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _chunker.ChunkAsync(content, string.Empty, sourceName));
    }

    [Fact]
    public async Task ChunkAsync_WithInvalidSourceName_ThrowsArgumentException()
    {
        // Arrange
        var content = "Some content";
        var sourceId = "test-doc-1";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _chunker.ChunkAsync(content, sourceId, string.Empty));
    }

    [Fact]
    public async Task ChunkAsync_WithVeryShortContent_ReturnsSingleChunk()
    {
        // Arrange
        var content = "Hi";
        var sourceId = "test-doc-1";
        var sourceName = "test.txt";

        _mockTokenCounter.Setup(t => t.CountTokens(content))
            .Returns(1); // Very short, 1 token

        // Act
        var result = await _chunker.ChunkAsync(content, sourceId, sourceName);

        // Assert
        Assert.NotNull(result);
        var chunks = result.ToList();
        Assert.Single(chunks);
        Assert.Equal(content, chunks[0].Content);
        Assert.Equal(sourceId, chunks[0].SourceId);
    }
}
