using AiSa.Application.Models;

namespace AiSa.Application;

/// <summary>
/// Provider-agnostic LLM client interface (ADR-0002).
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Generate a response from the LLM for the given prompt.
    /// </summary>
    /// <param name="prompt">User prompt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>LLM response text.</returns>
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Chat service interface following provider-agnostic pattern (ADR-0002).
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Process a chat request and return a response.
    /// </summary>
    /// <param name="request">Chat request containing user message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chat response with assistant message and correlation ID.</returns>
    Task<ChatResponse> ProcessChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

