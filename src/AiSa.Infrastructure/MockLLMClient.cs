using AiSa.Application;

namespace AiSa.Infrastructure;

/// <summary>
/// Mock LLM client implementation for testing and development.
/// Returns deterministic responses based on input.
/// </summary>
public class MockLLMClient : ILLMClient
{
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        // Deterministic response: for "hello", return "MOCK: hello ..." pattern
        var response = prompt.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase)
            ? "MOCK: hello! This is a deterministic mock response for testing purposes."
            : $"MOCK: {prompt.Trim()} ... This is a mock LLM response.";

        return Task.FromResult(response);
    }
}

