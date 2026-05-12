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
    private readonly IngestionContentGuard _contentGuard;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IDocumentChunker chunker,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IDocumentMetadataStore? metadataStore,
        IngestionContentGuard contentGuard,
        ActivitySource activitySource,
        ILogger<DocumentIngestionService> logger)
    {
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _metadataStore = metadataStore;
        _contentGuard = contentGuard ?? throw new ArgumentNullException(nameof(contentGuard));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        CancellationToken cancellationToken = default) =>
        IngestAsync(stream, sourceId, sourceName, updateExisting: false, governance: null, cancellationToken);

    public Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        bool updateExisting,
        CancellationToken cancellationToken = default) =>
        IngestAsync(stream, sourceId, sourceName, updateExisting, governance: null, cancellationToken);

    public async Task<IngestionResult> IngestAsync(
        Stream stream,
        string sourceId,
        string sourceName,
        bool updateExisting,
        IngestionGovernanceContext? governance,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Create telemetry span for ingestion
        using var activity = _activitySource.StartActivity("documents.ingest", ActivityKind.Internal);
        activity?.SetTag("documents.sourceId", sourceId);
        activity?.SetTag("documents.sourceName", sourceName);

        var gov = governance ?? IngestionGovernanceContext.Default();
        activity?.SetTag("documents.classification", gov.Classification.ToString());
        activity?.SetTag("documents.confidentialApproved", gov.ConfidentialApproved);

        try
        {

            // Log metadata only (ADR-0004)
            _logger.LogInformation(
                "Starting document ingestion. SourceId: {SourceId}, SourceName: {SourceName}, UpdateExisting: {UpdateExisting}, Classification: {Classification}",
                sourceId,
                sourceName,
                updateExisting,
                gov.Classification);

            if (gov.Classification == DataClassification.Restricted)
            {
                return Failed(sourceId, sourceName, gov, "Restricted classification cannot be indexed.");
            }

            if (gov.Classification == DataClassification.Confidential &&
                (!gov.ConfidentialApproved || string.IsNullOrWhiteSpace(gov.ApprovedBy)))
            {
                return Failed(sourceId, sourceName, gov, "Confidential documents require approval and approver metadata.");
            }

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

            if (_contentGuard.ShouldReject(content, out var guardReason))
            {
                _logger.LogWarning(
                    "Document ingestion rejected by content guard. SourceId: {SourceId}, ReasonCode: {ReasonCode}",
                    sourceId,
                    guardReason);
                return Failed(sourceId, sourceName, gov, "Content matched sensitive patterns and was not indexed.");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                activity?.SetTag("documents.chunkCount", 0);
                activity?.SetTag("documents.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.message", "Document content is empty");

                return Failed(sourceId, sourceName, gov, "Document content is empty");
            }

            // Step 2: Chunk document
            var chunks = await _chunker.ChunkAsync(content, sourceId, sourceName, cancellationToken);
            var chunksList = ApplyLineage(chunks.ToList(), gov);

            if (!chunksList.Any())
            {
                activity?.SetTag("documents.chunkCount", 0);
                activity?.SetTag("documents.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.message", "No chunks created");

                return Failed(sourceId, sourceName, gov, "No chunks created from document");
            }

            activity?.SetTag("documents.chunkCount", chunksList.Count);

            // Step 3: Generate embeddings for all chunks
            var chunkTexts = chunksList.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);
            var embeddingsList = embeddings.ToList();

            if (embeddingsList.Count != chunksList.Count)
            {
                var msg = $"Embedding count mismatch: expected {chunksList.Count}, got {embeddingsList.Count}";
                activity?.SetTag("documents.status", "failed");
                activity?.SetStatus(ActivityStatusCode.Error);
                activity?.SetTag("error.message", msg);

                return Failed(sourceId, sourceName, gov, msg, chunksList.Count);
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
                    IndexedAt = chunk.IndexedAt,
                    DocumentVersion = chunk.DocumentVersion,
                    Classification = chunk.Classification
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

            return Ok(sourceId, sourceName, gov, chunksList.Count);
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

            return Failed(sourceId, sourceName, gov, ex.Message);
        }
    }

    private static List<DocumentChunk> ApplyLineage(List<DocumentChunk> chunks, IngestionGovernanceContext gov)
    {
        var cls = gov.Classification.ToString();
        var ver = gov.DocumentVersion;
        return chunks.Select(c => new DocumentChunk
        {
            ChunkId = c.ChunkId,
            ChunkIndex = c.ChunkIndex,
            Content = c.Content,
            Vector = c.Vector,
            SourceId = c.SourceId,
            SourceName = c.SourceName,
            IndexedAt = c.IndexedAt,
            DocumentVersion = ver,
            Classification = cls
        }).ToList();
    }

    private static IngestionResult Ok(string sourceId, string sourceName, IngestionGovernanceContext gov, int chunkCount) =>
        new()
        {
            SourceId = sourceId,
            SourceName = sourceName,
            ChunkCount = chunkCount,
            Status = IngestionStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow,
            Classification = gov.Classification,
            Owner = gov.Owner,
            SourceType = gov.SourceType,
            ConfidentialApproved = gov.ConfidentialApproved,
            ApprovedBy = gov.ApprovedBy,
            ApprovedAt = gov.ApprovedAt,
            LastReviewedAt = gov.LastReviewedAt,
            ExpiresAt = gov.ExpiresAt
        };

    private static IngestionResult Failed(
        string sourceId,
        string sourceName,
        IngestionGovernanceContext gov,
        string message,
        int chunkCount = 0) =>
        new()
        {
            SourceId = sourceId,
            SourceName = sourceName,
            ChunkCount = chunkCount,
            Status = IngestionStatus.Failed,
            ErrorMessage = message,
            CompletedAt = DateTimeOffset.UtcNow,
            Classification = gov.Classification,
            Owner = gov.Owner,
            SourceType = gov.SourceType,
            ConfidentialApproved = gov.ConfidentialApproved,
            ApprovedBy = gov.ApprovedBy,
            ApprovedAt = gov.ApprovedAt,
            LastReviewedAt = gov.LastReviewedAt,
            ExpiresAt = gov.ExpiresAt
        };
}
