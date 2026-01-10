using AiSa.Application.Models;

namespace AiSa.Application;

/// <summary>
/// Chat service interface following provider-agnostic pattern (ADR-0002).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Process a chat request and return a response.
    /// </summary>
    /// <param name="request">Chat request containing user message and optional correlation ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chat response with assistant message and correlation ID.</returns>
    Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

