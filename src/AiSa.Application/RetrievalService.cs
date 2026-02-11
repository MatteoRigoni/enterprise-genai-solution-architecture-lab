using AiSa.Application.Models;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace AiSa.Application;

/// <summary>
/// Retrieval service implementation for querying vector store.
/// </summary>
public class RetrievalService : IRetrievalService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ActivitySource activitySource,
        ILogger<RetrievalService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<SearchResult>> RetrieveAsync(
        string query,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));
        if (topK <= 0)
            throw new ArgumentException("topK must be greater than 0", nameof(topK));

        // Create telemetry span for retrieval
        using var activity = _activitySource.StartActivity("retrieval.query", ActivityKind.Internal);
        activity?.SetTag("retrieval.query.length", query.Length);
        activity?.SetTag("retrieval.topK", topK);

        try
        {
            // Log metadata only (ADR-0004)
            _logger.LogInformation(
                "Starting retrieval. QueryLength: {QueryLength}, TopK: {TopK}",
                query.Length,
                topK);

            // Step 1: Generate embedding for query
            float[] queryEmbedding;
            try
            {
                queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("circuit", StringComparison.OrdinalIgnoreCase) || 
                                                   ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback when embedding service is unavailable
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.type", "EmbeddingServiceUnavailable");
                activity?.SetTag("fallback.used", true);
                
                _logger.LogWarning(
                    "Embedding service unavailable (circuit breaker or timeout). Returning empty results. QueryLength: {QueryLength}",
                    query.Length);
                
                return Enumerable.Empty<SearchResult>();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // Fallback when embedding times out
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.type", "EmbeddingTimeout");
                activity?.SetTag("fallback.used", true);
                
                _logger.LogWarning(
                    "Embedding service timeout. Returning empty results. QueryLength: {QueryLength}",
                    query.Length);
                
                return Enumerable.Empty<SearchResult>();
            }

            // Step 2: Search vector store
            var results = await _vectorStore.SearchAsync(queryEmbedding, topK, cancellationToken);
            var resultsList = results.ToList();

            // Log metadata only (ADR-0004: no raw content, only metadata)
            activity?.SetTag("retrieval.resultCount", resultsList.Count);
            if (resultsList.Any())
            {
                activity?.SetTag("retrieval.topScore", resultsList.First().Score);
                var sourceIds = resultsList.Select(r => r.Chunk.SourceId).Distinct().ToList();
                var chunkIdHashes = resultsList.Select(r => r.Chunk.ChunkId.GetHashCode().ToString("X")).ToList();
                _logger.LogInformation(
                    "Retrieval completed. ResultCount: {ResultCount}, TopScore: {TopScore}, SourceIds: {SourceIds}, ChunkIds: {ChunkIdHashes}",
                    resultsList.Count,
                    resultsList.First().Score,
                    string.Join(", ", sourceIds),
                    string.Join(", ", chunkIdHashes));
            }
            else
            {
                _logger.LogInformation("Retrieval returned no results. QueryLength: {QueryLength}", query.Length);
            }

            activity?.SetStatus(ActivityStatusCode.Ok);
            return resultsList;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);

            _logger.LogError(
                ex,
                "Retrieval failed. QueryLength: {QueryLength}, TopK: {TopK}",
                query.Length,
                topK);

            throw;
        }
    }
}
