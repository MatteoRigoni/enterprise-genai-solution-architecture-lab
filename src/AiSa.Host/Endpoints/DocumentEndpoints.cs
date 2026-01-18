using System.Diagnostics;
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

        // POST /api/documents - Upload and ingest document
        api.MapPost("/documents", async (
                HttpRequest request,
                IDocumentIngestionService ingestionService,
                IDocumentMetadataStore metadataStore,
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

                // Validate file type (text/plain, .txt extension)
                var allowedExtensions = new[] { ".txt" };
                var allowedContentTypes = new[] { "text/plain" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;

                if (!allowedExtensions.Contains(fileExtension) ||
                    (!string.IsNullOrWhiteSpace(contentType) && !allowedContentTypes.Contains(contentType)))
                {
                    // Log metadata only (ADR-0004)
                    logger.LogWarning(
                        "Invalid file type rejected. FileName: {FileName}, Extension: {Extension}, ContentType: {ContentType}",
                        file.FileName,
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

                // Generate source ID from filename and timestamp
                var sourceId = $"{Path.GetFileNameWithoutExtension(file.FileName)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
                var sourceName = file.FileName;

                // Log metadata only (ADR-0004: no raw content)
                logger.LogInformation(
                    "Starting document upload. FileName: {FileName}, Size: {Size}, SourceId: {SourceId}",
                    file.FileName,
                    file.Length,
                    sourceId);

                try
                {
                    // Ingest document
                    using var fileStream = file.OpenReadStream();
                    var ingestionResult = await ingestionService.IngestAsync(
                        fileStream,
                        sourceId,
                        sourceName,
                        cancellationToken);

                    // Store metadata
                    await metadataStore.StoreAsync(ingestionResult);

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
                        status = ingestionResult.Status.ToString().ToLowerInvariant(),
                        chunkCount = ingestionResult.ChunkCount,
                        indexedAt = ingestionResult.CompletedAt,
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
            .WithDescription("Accepts a text file (.txt) upload, processes it through the ingestion pipeline (chunking, embedding, indexing), and returns the ingestion status.")
            .WithTags("Documents")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .DisableAntiforgery(); // File uploads typically don't use antiforgery

        // GET /api/documents - List all ingested documents
        api.MapGet("/documents", async (
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
            .WithDescription("Returns a list of all documents that have been ingested, including metadata such as chunk count and indexing timestamp.")
            .WithTags("Documents")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}
