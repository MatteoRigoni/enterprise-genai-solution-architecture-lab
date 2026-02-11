using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AiSa.Application.Models;
using Microsoft.Extensions.Logging;

namespace AiSa.Application;

/// <summary>
/// Chat service implementation that processes chat requests using retrieval-augmented generation (RAG).
/// </summary>
public class ChatService : IChatService
{
    private readonly ILLMClient _llmClient;
    private readonly IRetrievalService _retrievalService;
    private readonly ICacheService _cacheService;
    private readonly ISecurityService _securityService;
    private readonly ActivitySource _activitySource;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ILLMClient llmClient,
        IRetrievalService retrievalService,
        ICacheService cacheService,
        ISecurityService securityService,
        ActivitySource activitySource,
        ILogger<ChatService> logger)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        // Input validation is handled at the API endpoint level (Program.cs) to return proper ProblemDetails.
        // This method assumes valid input and focuses on business logic.

        // Validate and sanitize input
        var validationResult = _securityService.ValidateInput(request.Message);
        if (!validationResult.IsValid)
        {
            throw new ArgumentException(validationResult.RejectionReason ?? "Input validation failed", nameof(request));
        }
        var sanitizedMessage = validationResult.SanitizedInput ?? request.Message;

        // Retrieve correlation ID from Activity Baggage (automatically propagated from parent span in /api/chat)
        // This ensures all child spans (retrieval.query, llm.generate, etc.) see the same correlation ID
        // Correlation ID is managed by CorrelationIdMiddleware via HTTP header X-Correlation-ID
        var correlationId = Activity.Current?.GetBaggageItem("correlation.id")
            ?? Activity.Current?.Id
            ?? Guid.NewGuid().ToString();

        // Step 1: Retrieve relevant document chunks (use sanitized message)
        const int topK = 3; // Number of chunks to retrieve
        IEnumerable<SearchResult> searchResults;
        try
        {
            searchResults = await _retrievalService.RetrieveAsync(sanitizedMessage, topK, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("circuit", StringComparison.OrdinalIgnoreCase) || 
                                              ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback when Azure AI Search is unavailable
            _logger.LogWarning(
                "Retrieval service unavailable (circuit breaker or timeout). Returning fallback response. CorrelationId: {CorrelationId}, Error: {ErrorType}",
                correlationId,
                ex.GetType().Name);
            
            return new ChatResponse
            {
                Response = "Document search is temporarily unavailable. Please try again later.",
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString(),
                Citations = Array.Empty<Citation>()
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Fallback when retrieval times out
            _logger.LogWarning(
                "Retrieval service timeout. Returning fallback response. CorrelationId: {CorrelationId}",
                correlationId);
            
            return new ChatResponse
            {
                Response = "Document search is temporarily unavailable. Please try again later.",
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString(),
                Citations = Array.Empty<Citation>()
            };
        }
        
        var resultsList = searchResults.ToList();

        // Step 2: Handle empty retrieval
        if (!resultsList.Any())
        {
            _logger.LogInformation(
                "No relevant documents found for query. Returning 'I don't know' response. CorrelationId: {CorrelationId}",
                correlationId);

            return new ChatResponse
            {
                Response = "I don't know based on provided documents.",
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString(),
                Citations = Array.Empty<Citation>()
            };
        }

        // Step 3: Build prompt with context and citations (use sanitized message)
        var prompt = BuildPromptWithContext(sanitizedMessage, resultsList);
        var citations = resultsList.Select(r => new Citation
        {
            SourceName = r.Chunk.SourceName,
            ChunkId = r.Chunk.ChunkId,
            Score = r.Score
        }).ToList();

        // Log metadata only (ADR-0004)
        _logger.LogInformation(
            "Building prompt with {ChunkCount} retrieved chunks. QueryLength: {QueryLength}, PromptLength: {PromptLength}, CorrelationId: {CorrelationId}",
            resultsList.Count,
            request.Message.Length,
            prompt.Length,
            correlationId);

        // Step 4: Generate LLM response with context
        using var activity = _activitySource.StartActivity("llm.generate", ActivityKind.Internal);
        activity?.SetTag("llm.prompt.length", prompt.Length);
        activity?.SetTag("llm.context.chunkCount", resultsList.Count);

        string responseText;
        try
        {
            var response = await _llmClient.GenerateAsync(prompt, cancellationToken);
            responseText = response ?? string.Empty;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("circuit", StringComparison.OrdinalIgnoreCase) || 
                                              ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            // Fallback when Azure OpenAI is unavailable
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", "CircuitBreakerOrTimeout");
            activity?.SetTag("fallback.used", true);
            
            _logger.LogWarning(
                "LLM service unavailable (circuit breaker or timeout). Returning fallback response. CorrelationId: {CorrelationId}, Error: {ErrorType}",
                correlationId,
                ex.GetType().Name);
            
            return new ChatResponse
            {
                Response = "I'm temporarily unable to process requests. Please try again later.",
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString(),
                Citations = citations // Still return citations even if LLM failed
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Fallback when LLM times out
            activity?.SetStatus(ActivityStatusCode.Error);
            activity?.SetTag("error.type", "Timeout");
            activity?.SetTag("fallback.used", true);
            
            _logger.LogWarning(
                "LLM service timeout. Returning fallback response. CorrelationId: {CorrelationId}",
                correlationId);
            
            return new ChatResponse
            {
                Response = "I'm temporarily unable to process requests. Please try again later.",
                CorrelationId = correlationId,
                MessageId = Guid.NewGuid().ToString(),
                Citations = citations
            };
        }

        var responseLength = responseText.Length;
        activity?.SetTag("llm.response.length", responseLength);
        activity?.SetStatus(ActivityStatusCode.Ok);

            var messageId = Guid.NewGuid().ToString();
            return new ChatResponse
            {
                Response = responseText,
                CorrelationId = correlationId,
                MessageId = messageId,
                Citations = citations
            };
    }

    private static string BuildPromptWithContext(string userQuery, IEnumerable<SearchResult> searchResults)
    {
        var prompt = new StringBuilder();

        // System instruction for RAG
        prompt.AppendLine("You are a helpful assistant that answers questions based on the provided context from documents.");
        prompt.AppendLine("When you reference information from the context, cite it using the format: [doc: {sourceName}, chunk: {chunkId}]");
        prompt.AppendLine();
        prompt.AppendLine("Context from documents:");
        prompt.AppendLine();

        // Add context chunks with citations
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

    private static string GenerateCacheKey(string prompt)
    {
        // Generate SHA256 hash of prompt for cache key
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(prompt));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
