using System.Diagnostics;
using AiSa.Application.Models;

namespace AiSa.Application;

/// <summary>
/// Chat service implementation that processes chat requests using an LLM client.
/// </summary>
public class ChatService : IChatService
{
    private readonly ILLMClient _llmClient;

    public ChatService(ILLMClient llmClient)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        // Input validation is handled at the API endpoint level (Program.cs) to return proper ProblemDetails.
        // This method assumes valid input and focuses on business logic.

        // Retrieve correlation ID from Activity Baggage (automatically propagated from parent span in /api/chat)
        // This ensures all child spans (retrieval.query, llm.generate, etc.) see the same correlation ID
        // Correlation ID is managed by CorrelationIdMiddleware via HTTP header X-Correlation-ID
        var correlationId = Activity.Current?.GetBaggageItem("correlation.id")
            ?? Activity.Current?.Id
            ?? Guid.NewGuid().ToString();

        // Call LLM to generate response
        var response = await _llmClient.GenerateAsync(request.Message, cancellationToken);

        return new ChatResponse
        {
            Response = response,
            CorrelationId = correlationId
        };
    }
}

