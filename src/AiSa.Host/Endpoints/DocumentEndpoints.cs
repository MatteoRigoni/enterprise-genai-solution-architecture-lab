using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiSa.Host.Endpoints;

/// <summary>
/// Document API endpoints.
/// </summary>
internal static class DocumentEndpoints
{
    /// <summary>
    /// Maps document endpoints to the API group.
    /// </summary>
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // POST /api/documents - Upload and ingest document (upsert by source name)
        api.MapPost("/documents", async (
                HttpRequest request,
                IDocumentIngestionService ingestionService,
                IDocumentMetadataStore metadataStore,
                IVectorStore vectorStore,
                ISecurityService securityService,
                ActivitySource activitySource,
                ILogger<Program> logger,
                CancellationToken cancellationToken) =>
            {
                // Validate multipart/form-data
                if (!request.HasFormContentType)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "Request must be multipart/form-data.");
                }

                var form = await request.ReadFormAsync(cancellationToken);
                var file = form.Files.GetFile("file");

                if (file == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "No file provided. Use form field name 'file'.");
                }

                // Sanitize file name
                var sanitizedFileName = securityService.SanitizeFileName(file.FileName);
                
                // Validate file type (text/plain, .txt extension)
                var allowedExtensions = new[] { ".txt", ".csv" };
                var allowedContentTypes = new[] { "text/plain" };
                var fileExtension = Path.GetExtension(sanitizedFileName).ToLowerInvariant();
                var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

                if (!allowedExtensions.Contains(fileExtension) ||
                    (!string.IsNullOrWhiteSpace(contentType) && !allowedContentTypes.Contains(contentType)))
                {
                    // Log metadata only (ADR-0004)
                    logger.LogWarning(
                        "Invalid file type rejected. FileName: {FileName}, Extension: {Extension}, ContentType: {ContentType}",
                        sanitizedFileName,
                        fileExtension,
                        contentType);

                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: $"Invalid file type. Only .txt files are supported. Received: {fileExtension}");
                }

                // Validate file size (max 10MB)
                const long maxFileSize = 10 * 1024 * 1024; // 10 MB
                if (file.Length > maxFileSize)
                {
                    logger.LogWarning(
                        "File too large rejected. FileName: {FileName}, Size: {Size}",
                        file.FileName,
                        file.Length);

                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: $"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)}MB.");
                }

                // Generate source ID from sanitized filename and timestamp
                var sourceId = $"{Path.GetFileNameWithoutExtension(sanitizedFileName)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                var sourceName = sanitizedFileName;
                var sourceNameNormalized = sourceName.Trim().ToLowerInvariant();

                // Log metadata only (ADR-0004: no raw content)
                logger.LogInformation(
                    "Starting document upload. FileName: {FileName}, Size: {Size}, SourceId: {SourceId}",
                    sanitizedFileName,
                    file.Length,
                    sourceId);

                try
                {
                    // Read stream once to support deterministic hash + ingestion
                    await using var uploadedFileStream = file.OpenReadStream();
                    using var memoryStream = new MemoryStream();
                    await uploadedFileStream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;

                    var contentHash = ComputeSha256(memoryStream);
                    memoryStream.Position = 0;

                    // Upsert by source name: if same file name already exists, remove old chunks from vector store
                    var existingLatest = await metadataStore.GetLatestBySourceNameAsync(sanitizedFileName);

                    // If content is unchanged, skip ingestion/indexing for idempotency.
                    if (existingLatest != null &&
                        string.Equals(existingLatest.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogInformation(
                            "Upload skipped because content hash is unchanged. SourceName: {SourceName}, DocumentId: {DocumentId}",
                            sanitizedFileName,
                            existingLatest.DocumentId);

                        return Results.Ok(new
                        {
                            documentId = existingLatest.DocumentId,
                            sourceName = existingLatest.SourceName,
                            sourceNameNormalized = existingLatest.SourceNameNormalized,
                            status = "unchanged",
                            dedupStatus = "unchanged",
                            version = existingLatest.Version,
                            chunkCount = existingLatest.ChunkCount,
                            indexedAt = existingLatest.IndexedAt,
                            contentHash,
                            errorMessage = (string?)null
                        });
                    }

                    if (existingLatest != null)
                    {
                        await vectorStore.DeleteBySourceIdAsync(existingLatest.DocumentId, cancellationToken);
                        logger.LogInformation(
                            "Replacing existing document. PreviousDocumentId: {PreviousDocumentId}, SourceName: {SourceName}",
                            existingLatest.DocumentId,
                            sanitizedFileName);
                    }
                    
                    // Ingest document
                    memoryStream.Position = 0;
                    var ingestionResult = await ingestionService.IngestAsync(
                        memoryStream,
                        sourceId,
                        sourceName,
                        updateExisting: existingLatest != null,
                        cancellationToken);

                    var metadataResult = new IngestionResult
                    {
                        SourceId = ingestionResult.SourceId,
                        SourceName = ingestionResult.SourceName,
                        ChunkCount = ingestionResult.ChunkCount,
                        Status = ingestionResult.Status,
                        ErrorMessage = ingestionResult.ErrorMessage,
                        CompletedAt = ingestionResult.CompletedAt,
                        SourceNameNormalized = sourceNameNormalized,
                        ContentHash = contentHash
                    };

                    // Store metadata (deprecates previous version if same source name)
                    await metadataStore.StoreAsync(metadataResult);

                    var latestMetadata = await metadataStore.GetByIdAsync(ingestionResult.SourceId);

                    // Log metadata only
                    logger.LogInformation(
                        "Document ingestion completed. SourceId: {SourceId}, Status: {Status}, ChunkCount: {ChunkCount}",
                        sourceId,
                        ingestionResult.Status,
                        ingestionResult.ChunkCount);

                    // Return ingestion result
                    var response = new
                    {
                        documentId = ingestionResult.SourceId,
                        sourceName = ingestionResult.SourceName,
                        sourceNameNormalized,
                        status = ingestionResult.Status.ToString().ToLowerInvariant(),
                        dedupStatus = existingLatest == null ? "new" : "updated",
                        version = latestMetadata?.Version ?? 1,
                        chunkCount = ingestionResult.ChunkCount,
                        indexedAt = ingestionResult.CompletedAt,
                        contentHash,
                        errorMessage = ingestionResult.ErrorMessage
                    };

                    if (ingestionResult.Status == IngestionStatus.Failed)
                    {
                        return Results.Problem(
                            statusCode: StatusCodes.Status500InternalServerError,
                            title: "Ingestion Failed",
                            detail: ingestionResult.ErrorMessage ?? "Document ingestion failed.");
                    }

                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Document ingestion error. SourceId: {SourceId}, FileName: {FileName}",
                        sourceId,
                        file.FileName);

                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Internal Server Error",
                        detail: "An error occurred while processing the document.");
                }
            })
            .WithName("UploadDocument")
            .WithSummary("Upload and ingest a document")
            .WithDescription("""
                Accepts a text file (.txt or .csv) upload and processes it through the ingestion pipeline.
                
                **Process:**
                1. Validates file type, size (max 10MB), and content
                2. Sanitizes file name to remove dangerous characters
                3. Chunks document into smaller pieces
                4. Generates embeddings for each chunk
                5. Indexes chunks in Azure AI Search vector store
                6. Returns ingestion status with chunk count
                
                **Rate Limiting:** 5 uploads per minute per user/IP
                
                **Supported Formats:** .txt and .csv files only (UTF-8 text)
                
                **Example Response:**
                ```json
                {
                  "documentId": "faq-20250127120000",
                  "sourceName": "faq.txt",
                  "status": "completed",
                  "chunkCount": 8,
                  "indexedAt": "2025-01-27T12:00:00Z"
                }
                ```
                """)
            .WithTags("Documents")
            .Produces<IngestionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .DisableAntiforgery(); // File uploads typically don't use antiforgery

        // GET /api/documents - List all ingested documents
        api.MapGet("/documents", async (
                HttpRequest request,
                IDocumentMetadataStore metadataStore,
                ILogger<Program> logger,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var documents = await metadataStore.GetAllAsync();
                    var documentsList = documents.ToList();

                    logger.LogInformation(
                        "Retrieved document list. Count: {Count}",
                        documentsList.Count);

                    var response = documentsList.Select(d => new
                    {
                        documentId = d.DocumentId,
                        sourceName = d.SourceName,
                        sourceNameNormalized = d.SourceNameNormalized,
                        version = d.Version,
                        contentHash = d.ContentHash,
                        chunkCount = d.ChunkCount,
                        indexedAt = d.IndexedAt,
                        status = d.Status.ToString().ToLowerInvariant()
                    }).ToList();

                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error retrieving document list");

                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Internal Server Error",
                        detail: "An error occurred while retrieving the document list.");
                }
            })
            .WithName("ListDocuments")
            .WithSummary("List all ingested documents")
            .WithDescription("""
                Returns a list of all documents that have been ingested into the knowledge base.
                
                **Returns:**
                - Document ID and source name
                - Ingestion status (completed, failed)
                - Number of chunks created
                - Indexing timestamp
                
                **Example Response:**
                ```json
                [
                  {
                    "documentId": "faq-20250127120000",
                    "sourceName": "faq.txt",
                    "chunkCount": 8,
                    "indexedAt": "2025-01-27T12:00:00Z",
                    "status": "completed"
                  }
                ]
                ```
                """)
            .WithTags("Documents")
            .Produces<IngestionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // PUT /api/documents/{id} - Update existing document
        api.MapPut("/documents/{documentId}", async (
                string documentId,
                HttpRequest request,
                IDocumentIngestionService ingestionService,
                IDocumentMetadataStore metadataStore,
                IVectorStore vectorStore,
                ISecurityService securityService,
                ActivitySource activitySource,
                ILogger<Program> logger,
                CancellationToken cancellationToken) =>
            {
                // Check if document exists
                var existingDoc = await metadataStore.GetByIdAsync(documentId);
                if (existingDoc == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Not Found",
                        detail: $"Document with ID '{documentId}' not found.");
                }

                // Validate multipart/form-data
                if (!request.HasFormContentType)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "Request must be multipart/form-data.");
                }

                var form = await request.ReadFormAsync(cancellationToken);
                var file = form.Files.GetFile("file");

                if (file == null)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "No file provided. Use form field name 'file'.");
                }

                // Validate file type
                var allowedExtensions = new[] { ".txt", ".csv" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: $"Invalid file type. Only .txt files are supported.");
                }

                // Validate file size
                const long maxFileSize = 10 * 1024 * 1024; // 10 MB
                if (file.Length > maxFileSize)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: $"File size exceeds maximum allowed size of {maxFileSize / (1024 * 1024)}MB.");
                }

                // Sanitize file name
                var sanitizedFileName = securityService.SanitizeFileName(file.FileName);

                // Generate new source ID for new version
                var newSourceId = $"{Path.GetFileNameWithoutExtension(sanitizedFileName)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                var sourceNameNormalized = sanitizedFileName.Trim().ToLowerInvariant();

                logger.LogInformation(
                    "Updating document. DocumentId: {DocumentId}, NewSourceId: {NewSourceId}, FileName: {FileName}",
                    documentId,
                    newSourceId,
                    sanitizedFileName);

                try
                {
                    // Delete old chunks from vector store
                    await vectorStore.DeleteBySourceIdAsync(existingDoc.DocumentId, cancellationToken);

                    // Ingest new version
                    await using var uploadedFileStream = file.OpenReadStream();
                    using var memoryStream = new MemoryStream();
                    await uploadedFileStream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;

                    var contentHash = ComputeSha256(memoryStream);
                    memoryStream.Position = 0;

                    var ingestionResult = await ingestionService.IngestAsync(
                        memoryStream,
                        newSourceId,
                        sanitizedFileName,
                        updateExisting: true,
                        cancellationToken);

                    var metadataResult = new IngestionResult
                    {
                        SourceId = ingestionResult.SourceId,
                        SourceName = ingestionResult.SourceName,
                        ChunkCount = ingestionResult.ChunkCount,
                        Status = ingestionResult.Status,
                        ErrorMessage = ingestionResult.ErrorMessage,
                        CompletedAt = ingestionResult.CompletedAt,
                        SourceNameNormalized = sourceNameNormalized,
                        ContentHash = contentHash
                    };

                    // Store metadata (versioning handled in metadata store)
                    await metadataStore.StoreAsync(metadataResult);

                    var latestMetadata = await metadataStore.GetByIdAsync(ingestionResult.SourceId);

                    if (ingestionResult.Status == IngestionStatus.Failed)
                    {
                        return Results.Problem(
                            statusCode: StatusCodes.Status500InternalServerError,
                            title: "Update Failed",
                            detail: ingestionResult.ErrorMessage ?? "Document update failed.");
                    }

                    var response = new
                    {
                        documentId = ingestionResult.SourceId,
                        sourceName = ingestionResult.SourceName,
                        sourceNameNormalized,
                        status = ingestionResult.Status.ToString().ToLowerInvariant(),
                        dedupStatus = "updated",
                        version = latestMetadata?.Version ?? existingDoc.Version + 1,
                        chunkCount = ingestionResult.ChunkCount,
                        indexedAt = ingestionResult.CompletedAt,
                        contentHash,
                        previousVersionId = documentId
                    };

                    return Results.Ok(response);
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Document update error. DocumentId: {DocumentId}, FileName: {FileName}",
                        documentId,
                        sanitizedFileName);

                    return Results.Problem(
                        statusCode: StatusCodes.Status500InternalServerError,
                        title: "Internal Server Error",
                        detail: "An error occurred while updating the document.");
                }
            })
            .WithName("UpdateDocument")
            .WithSummary("Update an existing document")
            .WithDescription("""
                Updates an existing document by creating a new version.
                
                **Process:**
                1. Validates document exists
                2. Deletes old chunks from vector store
                3. Ingests new version
                4. Marks old version as deprecated
                
                **Example Response:**
                ```json
                {
                  "documentId": "faq-20250127130000",
                  "sourceName": "faq.txt",
                  "status": "completed",
                  "chunkCount": 10,
                  "indexedAt": "2025-01-27T13:00:00Z",
                  "previousVersionId": "faq-20250127120000"
                }
                ```
                """)
            .WithTags("Documents")
            .Produces<IngestionResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .DisableAntiforgery();

        return app;
}

    private static string ComputeSha256(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
