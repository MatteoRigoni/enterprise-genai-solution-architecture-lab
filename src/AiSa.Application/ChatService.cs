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
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Message cannot be null or empty.", nameof(request));
        }

        // Retrieve correlation ID from Activity Baggage (automatically propagated from parent span in /api/chat)
        // This ensures all child spans (retrieval.query, llm.generate, etc.) see the same correlation ID
        var correlationId = Activity.Current?.GetBaggageItem("correlation.id")
            ?? request.CorrelationId
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

