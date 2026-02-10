using AiSa.Application.Models;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AiSa.Application;

/// <summary>
/// Document ingestion service implementation.
/// Orchestrates: file parsing → chunking → embedding → indexing.
/// </summary>
public class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentChunker _chunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IDocumentMetadataStore? _metadataStore;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IDocumentChunker chunker,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ActivitySource activitySource,
        ILogger<DocumentIngestionService> logger)
        : this(chunker, embeddingService, vectorStore, null, activitySource, logger)
    {
    }

    public DocumentIngestionService(
        IDocumentChunker chunker,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IDocumentMetadataStore? metadataStore,
        ActivitySource activitySource,
        ILogger<DocumentIngestionService> logger)
    {
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _metadataStore = metadataStore;
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        return await IngestAsync(stream, sourceId, sourceName, updateExisting: false, cancellationToken);
    }

    public async Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        bool updateExisting,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Create telemetry span for ingestion
        using var activity = _activitySource.StartActivity("documents.ingest", ActivityKind.Internal);
        activity?.SetTag("documents.sourceId", sourceId);
        activity?.SetTag("documents.sourceName", sourceName);

        try
        {
            // Log metadata only (ADR-0004)
            _logger.LogInformation(
                "Starting document ingestion. SourceId: {SourceId}, SourceName: {SourceName}, UpdateExisting: {UpdateExisting}",
                sourceId,
                sourceName,
                updateExisting);

            // If updating existing, check for previous version
            if (updateExisting && _metadataStore != null)
            {
                var existingLatest = await _metadataStore.GetLatestBySourceNameAsync(sourceName);
                if (existingLatest != null)
                {
                    _logger.LogInformation(
                        "Updating existing document. PreviousVersionId: {PreviousVersionId}, PreviousVersion: {PreviousVersion}",
                        existingLatest.DocumentId,
                        existingLatest.Version);
                }
            }

            // Step 1: Parse file content (plain text for now)
            string content;
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                content = await reader.ReadToEndAsync(cancellationToken);
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                var errorResult = new IngestionResult
                {
                    SourceId = sourceId,
                    SourceName = sourceName,
                    ChunkCount = 0,
                    Status = IngestionStatus.Failed,
                    ErrorMessage = "Document content is empty",
                    CompletedAt = DateTimeOffset.UtcNow
                };

                activity?.SetTag("documents.chunkCount", 0);
                activity?.SetTag("documents.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.message", "Document content is empty");

                return errorResult;
            }

            // Step 2: Chunk document
            var chunks = await _chunker.ChunkAsync(content, sourceId, sourceName, cancellationToken);
            var chunksList = chunks.ToList();

            if (!chunksList.Any())
            {
                var errorResult = new IngestionResult
                {
                    SourceId = sourceId,
                    SourceName = sourceName,
                    ChunkCount = 0,
                    Status = IngestionStatus.Failed,
                    ErrorMessage = "No chunks created from document",
                    CompletedAt = DateTimeOffset.UtcNow
                };

                activity?.SetTag("documents.chunkCount", 0);
                activity?.SetTag("documents.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.message", "No chunks created");

                return errorResult;
            }

            activity?.SetTag("documents.chunkCount", chunksList.Count);

            // Step 3: Generate embeddings for all chunks
            var chunkTexts = chunksList.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);
            var embeddingsList = embeddings.ToList();

            if (embeddingsList.Count != chunksList.Count)
            {
                var errorResult = new IngestionResult
                {
                    SourceId = sourceId,
                    SourceName = sourceName,
                    ChunkCount = chunksList.Count,
                    Status = IngestionStatus.Failed,
                    ErrorMessage = $"Embedding count mismatch: expected {chunksList.Count}, got {embeddingsList.Count}",
                    CompletedAt = DateTimeOffset.UtcNow
                };

                activity?.SetTag("documents.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.message", errorResult.ErrorMessage);

                return errorResult;
            }

            // Step 4: Combine chunks with embeddings
            var chunksWithEmbeddings = chunksList.Zip(embeddingsList, (chunk, embedding) =>
            {
                return new DocumentChunk
                {
                    ChunkId = chunk.ChunkId,
                    ChunkIndex = chunk.ChunkIndex,
                    Content = chunk.Content,
                    Vector = embedding,
                    SourceId = chunk.SourceId,
                    SourceName = chunk.SourceName,
                    IndexedAt = chunk.IndexedAt
                };
            }).ToList();

            // Step 5: Index chunks in vector store
            await _vectorStore.AddDocumentsAsync(chunksWithEmbeddings, cancellationToken);

            var duration = DateTimeOffset.UtcNow - startTime;
            activity?.SetTag("documents.durationMs", duration.TotalMilliseconds);
            activity?.SetTag("documents.status", "completed");
            activity?.SetStatus(ActivityStatusCode.Ok);

            // Log metadata only
            _logger.LogInformation(
                "Document ingestion completed. SourceId: {SourceId}, ChunkCount: {ChunkCount}, DurationMs: {DurationMs}",
                sourceId,
                chunksList.Count,
                duration.TotalMilliseconds);

            return new IngestionResult
            {
                SourceId = sourceId,
                SourceName = sourceName,
                ChunkCount = chunksList.Count,
                Status = IngestionStatus.Completed,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            activity?.SetTag("documents.durationMs", duration.TotalMilliseconds);
            activity?.SetTag("documents.status", "failed");
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);

            _logger.LogError(
                ex,
                "Document ingestion failed. SourceId: {SourceId}, SourceName: {SourceName}, DurationMs: {DurationMs}",
                sourceId,
                sourceName,
                duration.TotalMilliseconds);

            return new IngestionResult
            {
                SourceId = sourceId,
                SourceName = sourceName,
                ChunkCount = 0,
                Status = IngestionStatus.Failed,
                ErrorMessage = ex.Message,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
