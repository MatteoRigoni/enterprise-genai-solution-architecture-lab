using AiSa.Application;
using AiSa.Application.Models;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiSa.Infrastructure;

/// <summary>
/// Azure AI Search implementation of IVectorStore (ADR-0003).
/// </summary>
public class AzureSearchVectorStore : IVectorStore
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;
    private readonly string _vectorSimilarityMetric;
    private readonly ILogger<AzureSearchVectorStore> _logger;
    private static readonly SemaphoreSlim _indexCreationLock = new(1, 1);

    public AzureSearchVectorStore(
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchVectorStore> logger)
    {
        if (options?.Value == null)
            throw new ArgumentNullException(nameof(options));

        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException("AzureSearch:Endpoint is required", nameof(options));
        if (string.IsNullOrWhiteSpace(config.IndexName))
            throw new ArgumentException("AzureSearch:IndexName is required", nameof(options));

        _indexName = config.IndexName;
        _vectorSimilarityMetric = config.VectorSimilarityMetric ?? "cosine";
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate metric
        var validMetrics = new[] { "cosine", "dotProduct", "euclidean" };
        if (!validMetrics.Contains(_vectorSimilarityMetric, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"VectorSimilarityMetric must be one of: {string.Join(", ", validMetrics)}. Got: {_vectorSimilarityMetric}",
                nameof(options));
        }

        var endpoint = new Uri(config.Endpoint);
        var credential = !string.IsNullOrWhiteSpace(config.ApiKey)
            ? new AzureKeyCredential(config.ApiKey)
            : throw new ArgumentException("AzureSearch:ApiKey is required", nameof(options));

        _indexClient = new SearchIndexClient(endpoint, credential);
        _searchClient = new SearchClient(endpoint, _indexName, credential);
    }

    public async Task AddDocumentsAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var chunksList = chunks.ToList();
        if (!chunksList.Any())
            return;

        // Ensure index exists
        await EnsureIndexExistsAsync(cancellationToken);

        // Log metadata only (ADR-0004: no raw content)
        var sourceIds = chunksList.Select(c => c.SourceId).Distinct().ToList();
        var chunkIds = chunksList.Select(c => c.ChunkId).ToList();
        _logger.LogInformation(
            "Adding {ChunkCount} chunks to index. SourceIds: {SourceIds}, ChunkIds: {ChunkIdHashes}",
            chunksList.Count,
            string.Join(", ", sourceIds),
            string.Join(", ", chunkIds.Select(id => id.GetHashCode().ToString("X"))));

        // Convert chunks to Azure Search documents
        var documents = chunksList.Select(chunk => new SearchDocument
        {
            ["id"] = chunk.ChunkId,
            ["chunkId"] = chunk.ChunkId,
            ["chunkIndex"] = chunk.ChunkIndex,
            ["content"] = chunk.Content,
            ["contentVector"] = chunk.Vector,
            ["sourceId"] = chunk.SourceId,
            ["sourceName"] = chunk.SourceName,
            ["indexedAt"] = chunk.IndexedAt
        }).ToList();

        // Upload documents in batches
        var batchSize = 100; // Azure AI Search batch limit
        for (int i = 0; i < documents.Count; i += batchSize)
        {
            var batch = documents.Skip(i).Take(batchSize);
            var batchActions = batch.Select(doc => IndexDocumentsAction.Upload(doc)).ToArray();
            var batchResult = await _searchClient.IndexDocumentsAsync(
                IndexDocumentsBatch.Create<SearchDocument>(batchActions),
                cancellationToken: cancellationToken);

            // Log errors (metadata only)
            if (batchResult.Value.Results.Any(r => !r.Succeeded))
            {
                var failed = batchResult.Value.Results.Where(r => !r.Succeeded).ToList();
                _logger.LogWarning(
                    "Failed to index {FailedCount} documents. Errors: {Errors}",
                    failed.Count,
                    string.Join("; ", failed.Select(f => $"{f.Key}: {f.ErrorMessage}")));
            }
        }

        _logger.LogInformation(
            "Successfully indexed {ChunkCount} chunks for sources: {SourceIds}",
            chunksList.Count,
            string.Join(", ", sourceIds));
    }

    public async Task<IEnumerable<SearchResult>> SearchAsync(float[] queryVector, int topK, CancellationToken cancellationToken = default)
    {
        if (queryVector == null || queryVector.Length == 0)
            throw new ArgumentException("Query vector cannot be null or empty", nameof(queryVector));
        if (topK <= 0)
            throw new ArgumentException("topK must be greater than 0", nameof(topK));

        // Ensure index exists
        await EnsureIndexExistsAsync(cancellationToken);

        // Log metadata only (ADR-0004)
        _logger.LogInformation(
            "Searching index with vector dimension {VectorDimension}, topK: {TopK}, metric: {Metric}",
            queryVector.Length,
            topK,
            _vectorSimilarityMetric);

        // Create vector search query
        var searchOptions = new SearchOptions
        {
            VectorSearch = new()
            {
                Queries = { new VectorizedQuery(queryVector)
                {
                    KNearestNeighborsCount = topK,
                    Fields = { "contentVector" }
                }}
            },
            Size = topK,
            Select = { "chunkId", "chunkIndex", "content", "sourceId", "sourceName", "indexedAt" }
        };

        var searchResults = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);

        var results = new List<SearchResult>();
        await foreach (var result in searchResults.Value.GetResultsAsync())
        {
            if (result.Score.HasValue)
            {
                var doc = result.Document;
                var chunk = new DocumentChunk
                {
                    ChunkId = doc["chunkId"].ToString() ?? string.Empty,
                    ChunkIndex = doc["chunkIndex"] != null ? Convert.ToInt32(doc["chunkIndex"]) : 0,
                    Content = doc["content"].ToString() ?? string.Empty,
                    Vector = Array.Empty<float>(), // Not needed in search results
                    SourceId = doc["sourceId"].ToString() ?? string.Empty,
                    SourceName = doc["sourceName"].ToString() ?? string.Empty,
                    IndexedAt = doc["indexedAt"] != null && DateTimeOffset.TryParse(doc["indexedAt"].ToString(), out var dt)
                        ? dt
                        : DateTimeOffset.UtcNow
                };

                results.Add(new SearchResult
                {
                    Chunk = chunk,
                    Score = result.Score.Value
                });
            }
        }

        // Log metadata only
        _logger.LogInformation(
            "Search returned {ResultCount} results. Top score: {TopScore}",
            results.Count,
            results.FirstOrDefault()?.Score ?? 0);

        return results.OrderByDescending(r => r.Score);
    }

    public async Task DeleteBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be null or empty", nameof(sourceId));

        _logger.LogInformation("Deleting chunks for sourceId: {SourceId}", sourceId);

        // Search for all documents with this sourceId
        var searchOptions = new SearchOptions
        {
            Filter = $"sourceId eq '{sourceId.Replace("'", "''")}'", // Escape single quotes
            Select = { "id" },
            Size = 10000 // Azure AI Search limit
        };

        var searchResults = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
        var idsToDelete = new List<string>();

        await foreach (var result in searchResults.Value.GetResultsAsync())
        {
            if (result.Document.TryGetValue("id", out var idObj) && idObj != null)
            {
                idsToDelete.Add(idObj.ToString() ?? string.Empty);
            }
        }

        if (idsToDelete.Any())
        {
            // Delete in batches
            var batchSize = 100;
            for (int i = 0; i < idsToDelete.Count; i += batchSize)
            {
                var batch = idsToDelete.Skip(i).Take(batchSize);
                var batchActions = batch.Select(id => IndexDocumentsAction.Delete(new SearchDocument { ["id"] = id })).ToArray();
                await _searchClient.IndexDocumentsAsync(
                    IndexDocumentsBatch.Create<SearchDocument>(batchActions),
                    cancellationToken: cancellationToken);
            }

            _logger.LogInformation("Deleted {Count} chunks for sourceId: {SourceId}", idsToDelete.Count, sourceId);
        }
        else
        {
            _logger.LogInformation("No chunks found for sourceId: {SourceId}", sourceId);
        }
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        // Use lock to prevent concurrent index creation attempts
        await _indexCreationLock.WaitAsync(cancellationToken);
        try
        {
            // Check if index exists
            try
            {
                await _indexClient.GetIndexAsync(_indexName, cancellationToken);
                return; // Index exists
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                // Index doesn't exist, create it
                _logger.LogInformation(
                    "Creating Azure AI Search index: {IndexName} with similarity metric: {Metric}",
                    _indexName,
                    _vectorSimilarityMetric);

                var index = new SearchIndex(_indexName)
                {
                    Fields =
                    {
                        new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                        new SimpleField("chunkId", SearchFieldDataType.String) { IsFilterable = true },
                        new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsFilterable = true },
                        new SearchableField("content") { AnalyzerName = LexicalAnalyzerName.StandardLucene },
                        new VectorSearchField("contentVector", 1536, "default") // 1536 dimensions for text-embedding-ada-002
                        {
                            VectorSearchProfileName = "default"
                        },
                        new SimpleField("sourceId", SearchFieldDataType.String) { IsFilterable = true },
                        new SimpleField("sourceName", SearchFieldDataType.String) { IsFilterable = true },
                        new SimpleField("indexedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true }
                    },
                    VectorSearch = new()
                    {
                        Profiles =
                        {
                            new VectorSearchProfile("default", "default")
                        },
                        Algorithms =
                        {
                            new HnswAlgorithmConfiguration("default")
                        }
                    }
                };

                await _indexClient.CreateIndexAsync(index, cancellationToken);
                _logger.LogInformation("Successfully created index: {IndexName}", _indexName);
            }
        }
        finally
        {
            _indexCreationLock.Release();
        }
    }
}

/// <summary>
/// Configuration options for Azure AI Search.
/// </summary>
public class AzureSearchOptions
{
    /// <summary>
    /// Azure AI Search service endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Index name.
    /// </summary>
    public string IndexName { get; set; } = "aisa-documents";

    /// <summary>
    /// API key for authentication (or use managed identity later).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Vector similarity metric: "cosine" (default), "dotProduct", or "euclidean".
    /// - cosine: Measures angle between vectors, ignores magnitude (0-1 range typically)
    /// - dotProduct: Includes magnitude, equivalent to cosine for normalized vectors
    /// - euclidean: L2 distance, lower values = more similar
    /// </summary>
    public string VectorSimilarityMetric { get; set; } = "cosine";
}
