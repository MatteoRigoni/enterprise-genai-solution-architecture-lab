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

        // Generate correlation ID if not provided
        var correlationId = request.CorrelationId ?? System.Diagnostics.Activity.Current?.Id ?? Guid.NewGuid().ToString();

        // Call LLM to generate response
        var response = await _llmClient.GenerateAsync(request.Message, cancellationToken);

        return new ChatResponse
        {
            Response = response,
            CorrelationId = correlationId
        };
    }
}

