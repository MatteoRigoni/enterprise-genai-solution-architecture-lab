using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AiSa.Application.Models;
using AiSa.Application.ToolCalling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IOptions<ToolCallingOptions> _toolCallingOptions;
    private readonly IToolCallParser _toolCallParser;
    private readonly IToolRegistry _toolRegistry;
    private readonly IToolInputValidatorRegistry _toolInputValidatorRegistry;

    public ChatService(
        ILLMClient llmClient,
        IRetrievalService retrievalService,
        ICacheService cacheService,
        ISecurityService securityService,
        ActivitySource activitySource,
        ILogger<ChatService> logger,
        IOptions<ToolCallingOptions> toolCallingOptions,
        IToolCallParser toolCallParser,
        IToolRegistry toolRegistry,
        IToolInputValidatorRegistry toolInputValidatorRegistry)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _retrievalService = retrievalService ?? throw new ArgumentNullException(nameof(retrievalService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolCallingOptions = toolCallingOptions ?? throw new ArgumentNullException(nameof(toolCallingOptions));
        _toolCallParser = toolCallParser ?? throw new ArgumentNullException(nameof(toolCallParser));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _toolInputValidatorRegistry = toolInputValidatorRegistry ?? throw new ArgumentNullException(nameof(toolInputValidatorRegistry));
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
        var toolCallingEnabled = _toolCallingOptions.Value.Enabled;
        var prompt = BuildPromptWithContext(sanitizedMessage, resultsList, toolCallingEnabled);
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

        if (toolCallingEnabled
            && _toolCallParser.TryParse(responseText, out var proposal)
            && proposal != null)
        {
            if (_toolCallingOptions.Value.MaxToolCallsPerRequest < 1)
            {
                _logger.LogWarning("Tool call blocked by MaxToolCallsPerRequest. CorrelationId: {CorrelationId}",
                    correlationId);
                responseText = "Tool execution is disabled by configuration.";
            }
            else if (!_toolRegistry.TryGetHandler(proposal.Name, out var handler) || handler == null)
            {
                _logger.LogWarning(
                    "Disallowed tool proposal (not allow-listed). ToolName: {ToolName}, CorrelationId: {CorrelationId}",
                    proposal.Name,
                    correlationId);
                responseText = "That tool is not available.";
            }
            else if (!_toolInputValidatorRegistry.TryGetValidator(proposal.Name, out var inputValidator) ||
                     inputValidator == null)
            {
                _logger.LogWarning(
                    "Tool has no input validator registered. ToolName: {ToolName}, CorrelationId: {CorrelationId}",
                    proposal.Name,
                    correlationId);
                responseText = "That tool is not available.";
            }
            else
            {
                var validation = inputValidator.Validate(proposal);
                if (!validation.IsValid)
                {
                    _logger.LogWarning(
                        "Tool input validation failed. ToolName: {ToolName}, CorrelationId: {CorrelationId}",
                        proposal.Name,
                        correlationId);
                    responseText = validation.UserSafeMessage ?? "Request could not be processed.";
                }
                else
                {
                    activity?.SetTag("tool.name", proposal.Name);
                    responseText = await handler.ExecuteAsync(proposal, cancellationToken);
                }
            }
        }

        var messageId = Guid.NewGuid().ToString();
        return new ChatResponse
        {
            Response = responseText,
            CorrelationId = correlationId,
            MessageId = messageId,
            Citations = citations
        };
    }

    private static string BuildPromptWithContext(
        string userQuery,
        IEnumerable<SearchResult> searchResults,
        bool includeToolCallingInstructions = false)
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
        if (includeToolCallingInstructions)
        {
            prompt.AppendLine(ToolCallingPrompt.AllowedToolsSectionHeader);
            prompt.AppendLine("- GetOrderStatus — arguments: { \"orderId\": string }");
            prompt.AppendLine("- CreateSupportTicket — arguments: { \"subject\": string, \"details\": string }");
            prompt.AppendLine();
            prompt.AppendLine("You may call at most one tool when it is the most helpful way to answer. If you choose to use a tool, reply with exactly one line: only a <tool_call>...</tool_call> block (valid JSON inside the tags), and nothing else.");
            prompt.AppendLine("<tool_call>{\"name\":\"GetOrderStatus\",\"arguments\":{\"orderId\":\"...\"}}</tool_call>");
            prompt.AppendLine("or");
            prompt.AppendLine(
                "<tool_call>{\"name\":\"CreateSupportTicket\",\"arguments\":{\"subject\":\"...\",\"details\":\"...\"}}</tool_call>");
            prompt.AppendLine();
            prompt.AppendLine(
                "If you do not use a tool, answer in plain text using only the document excerpts above. If those excerpts are not sufficient for the question, say 'I don't know based on provided documents.'");
            prompt.AppendLine(
                "Note: live order status or opening a support ticket may not appear in the excerpts; when the user clearly asks for those, deciding to use the matching tool is appropriate.");
        }
        else
        {
            prompt.AppendLine(
                "Answer based on the provided context. If the context doesn't contain enough information to answer the question, say 'I don't know based on provided documents.'");
        }

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
