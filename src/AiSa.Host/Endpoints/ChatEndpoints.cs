using System.Diagnostics;
using System.Text;
using AiSa.Application;
using AiSa.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace AiSa.Host.Endpoints;

/// <summary>
/// Chat API endpoints.
/// </summary>
internal static class ChatEndpoints
{
    /// <summary>
    /// Maps chat endpoints to the API group.
    /// </summary>
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapPost("/chat", async (
                ChatRequest request,
                IChatService chatService,
                ISecurityService securityService,
                ActivitySource activitySource,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                // Minimal input validation as a 400 (ProblemDetails is still RFC7807)
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: "The request is invalid. Please check your input and try again.");
                }

                // Security validation
                var validationResult = securityService.ValidateInput(request.Message);
                if (!validationResult.IsValid)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Bad Request",
                        detail: validationResult.RejectionReason ?? "Input validation failed.");
                }

                // Child span for application work; the incoming HTTP span already exists
                using var activity = activitySource.StartActivity("chat.handle", ActivityKind.Internal);

                // Low-cardinality metadata only (avoid logging raw prompts)
                activity?.SetTag("chat.message.length", request.Message.Length);

                var response = await chatService.ProcessChatAsync(request, cancellationToken);

                activity?.SetTag("chat.response.length", response.Response?.Length ?? 0);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return Results.Ok(response);
            })
            .WithName("ChatApi")
            .WithSummary("Send a chat message")
            .WithDescription("""
                Processes a chat message using Retrieval-Augmented Generation (RAG) and returns a response from the LLM.
                
                **Process:**
                1. Validates and sanitizes input to prevent prompt injection
                2. Retrieves relevant document chunks using semantic search
                3. Builds a prompt with context and citations
                4. Generates LLM response (with caching support)
                5. Returns response with citations referencing source documents
                
                **Rate Limiting:** 10 requests per minute per user/IP
                
                **Example Request:**
                ```json
                {
                  "message": "What is the storage limit on the free plan?"
                }
                ```
                
                **Example Response:**
                ```json
                {
                  "response": "The free plan includes 5 GB of total storage.",
                  "correlationId": "01234567-89ab-cdef-0123-456789abcdef",
                  "citations": [
                    {
                      "sourceName": "faq.txt",
                      "chunkId": "chunk-001",
                      "score": 0.95
                    }
                  ]
                }
                ```
                """)
            .WithTags("Chat")
            .Produces<ChatResponse>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests, "application/json")
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .Accepts<ChatRequest>("application/json");

        // Streaming endpoint (Server-Sent Events)
        api.MapPost("/chat/stream", async (
                ChatRequest request,
                IChatService chatService,
                ISecurityService securityService,
                ILLMClient llmClient,
                IRetrievalService retrievalService,
                ActivitySource activitySource,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                // Minimal input validation
                if (string.IsNullOrWhiteSpace(request.Message))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await httpContext.Response.WriteAsync("data: {\"error\":\"Message cannot be empty\"}\n\n", cancellationToken);
                    return;
                }

                // Security validation
                var validationResult = securityService.ValidateInput(request.Message);
                if (!validationResult.IsValid)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                    var errorJson = $"{{\"error\":\"{validationResult.RejectionReason?.Replace("\"", "\\\"") ?? "Input validation failed"}\"}}";
                    await httpContext.Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
                    return;
                }

                var sanitizedMessage = validationResult.SanitizedInput ?? request.Message;

                // Set up SSE headers
                httpContext.Response.ContentType = "text/event-stream";
                httpContext.Response.Headers.CacheControl = "no-cache";
                httpContext.Response.Headers.Connection = "keep-alive";

                var correlationId = Activity.Current?.GetBaggageItem("correlation.id")
                    ?? Activity.Current?.Id
                    ?? Guid.NewGuid().ToString();

                using var activity = activitySource.StartActivity("chat.stream", ActivityKind.Internal);
                activity?.SetTag("chat.message.length", request.Message.Length);
                activity?.SetTag("chat.streaming", true);

                try
                {
                    // Step 1: Retrieve relevant document chunks
                    const int topK = 3;
                    var searchResults = await retrievalService.RetrieveAsync(sanitizedMessage, topK, cancellationToken);
                    var resultsList = searchResults.ToList();

                    // Step 2: Build prompt with context
                    var prompt = BuildPromptWithContext(sanitizedMessage, resultsList);
                    var citations = resultsList.Select(r => new Citation
                    {
                        SourceName = r.Chunk.SourceName,
                        ChunkId = r.Chunk.ChunkId,
                        Score = r.Score
                    }).ToList();

                    // Send initial metadata
                    var metadataJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        correlationId,
                        messageId = Guid.NewGuid().ToString(),
                        citations = citations
                    });
                    await httpContext.Response.WriteAsync($"data: {{\"type\":\"metadata\",\"data\":{metadataJson}}}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);

                    // Step 3: Stream LLM response
                    var fullResponse = new StringBuilder();
                    await foreach (var chunk in llmClient.GenerateStreamAsync(prompt, cancellationToken))
                    {
                        fullResponse.Append(chunk);
                        
                        // Send chunk as SSE
                        var chunkJson = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            type = "chunk",
                            data = chunk
                        });
                        await httpContext.Response.WriteAsync($"data: {chunkJson}\n\n", cancellationToken);
                        await httpContext.Response.Body.FlushAsync(cancellationToken);
                    }

                    // Send completion marker
                    await httpContext.Response.WriteAsync("data: {\"type\":\"done\"}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);

                    activity?.SetTag("chat.response.length", fullResponse.Length);
                    activity?.SetStatus(ActivityStatusCode.Ok);
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error);
                    activity?.SetTag("error.type", ex.GetType().Name);
                    
                    var errorJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        type = "error",
                        error = "An error occurred while generating the response."
                    });
                    await httpContext.Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }
            })
            .WithName("ChatStreamApi")
            .WithSummary("Stream chat response")
            .WithDescription("""
                Streams a chat response using Server-Sent Events (SSE).
                Returns chunks of text as they are generated by the LLM.
                
                **Response Format:**
                - `{"type":"metadata","data":{...}}` - Initial metadata with citations
                - `{"type":"chunk","data":"text"}` - Text chunks as they arrive
                - `{"type":"done"}` - Stream completion marker
                - `{"type":"error","error":"message"}` - Error occurred
                """)
            .WithTags("Chat")
            .Accepts<ChatRequest>("application/json");

        return app;
    }

    private static string BuildPromptWithContext(string userQuery, IEnumerable<SearchResult> searchResults)
    {
        var prompt = new StringBuilder();

        prompt.AppendLine("You are a helpful assistant that answers questions based on the provided context from documents.");
        prompt.AppendLine("When you reference information from the context, cite it using the format: [doc: {sourceName}, chunk: {chunkId}]");
        prompt.AppendLine();
        prompt.AppendLine("Context from documents:");
        prompt.AppendLine();

        foreach (var result in searchResults)
        {
            var chunk = result.Chunk;
            var citation = $"[doc: {chunk.SourceName}, chunk: {chunk.ChunkId}]";
            prompt.AppendLine($"--- {citation} ---");
            prompt.AppendLine(chunk.Content);
            prompt.AppendLine();
        }

        prompt.AppendLine("--- End of context ---");
        prompt.AppendLine();
        prompt.AppendLine($"Question: {userQuery}");
        prompt.AppendLine();
        prompt.AppendLine("Answer based on the provided context. If the context doesn't contain enough information to answer the question, say 'I don't know based on provided documents.'");

        return prompt.ToString();
    }
}


