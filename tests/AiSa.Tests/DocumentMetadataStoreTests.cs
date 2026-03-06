using AiSa.Application;
using AiSa.Application.Models;

namespace AiSa.Tests;

public class DocumentMetadataStoreTests
{
    [Fact]
    public async Task StoreAsync_WithSameSourceNameCaseInsensitive_TracksVersionIncrement()
    {
        var store = new InMemoryDocumentMetadataStore();

        await store.StoreAsync(new IngestionResult
        {
            SourceId = "doc-1",
            SourceName = "FAQ.TXT",
            SourceNameNormalized = "faq.txt",
            ChunkCount = 3,
            Status = IngestionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            ContentHash = "hash-a"
        });

        await store.StoreAsync(new IngestionResult
        {
            SourceId = "doc-2",
            SourceName = "faq.txt",
            SourceNameNormalized = "faq.txt",
            ChunkCount = 4,
            Status = IngestionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            ContentHash = "hash-b"
        });

        var latest = await store.GetLatestBySourceNameAsync(" FaQ.TxT ");

        Assert.NotNull(latest);
        Assert.Equal("doc-2", latest.DocumentId);
        Assert.Equal(2, latest.Version);
        Assert.Equal("hash-b", latest.ContentHash);
    }

    [Fact]
    public async Task GetAllAsync_ExcludesDeprecatedVersions()
    {
        var store = new InMemoryDocumentMetadataStore();

        await store.StoreAsync(new IngestionResult
        {
            SourceId = "doc-1",
            SourceName = "faq.txt",
            ChunkCount = 3,
            Status = IngestionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            ContentHash = "hash-a"
        });

        await store.StoreAsync(new IngestionResult
        {
            SourceId = "doc-2",
            SourceName = "faq.txt",
            ChunkCount = 5,
            Status = IngestionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            ContentHash = "hash-b"
        });

        var all = (await store.GetAllAsync()).ToList();
        var firstVersion = await store.GetByIdAsync("doc-1");

        Assert.Single(all);
        Assert.Equal("doc-2", all[0].DocumentId);
        Assert.NotNull(firstVersion);
        Assert.True(firstVersion.IsDeprecated);
    }
}
