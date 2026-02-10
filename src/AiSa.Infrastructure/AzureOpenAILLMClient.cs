using AiSa.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AiSa.Infrastructure;

/// <summary>
/// Azure OpenAI LLM client implementation using REST API (Chat Completions).
/// </summary>
public class AzureOpenAILLMClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _deploymentName;
    private readonly ILogger<AzureOpenAILLMClient> _logger;

    public AzureOpenAILLMClient(
        IOptions<AzureOpenAIOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureOpenAILLMClient> logger)
    {
        if (options?.Value == null)
            throw new ArgumentNullException(nameof(options));

        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException("AzureOpenAI:Endpoint is required", nameof(options));
        if (string.IsNullOrWhiteSpace(config.LLMDeploymentName))
            throw new ArgumentException("AzureOpenAI:LLMDeploymentName is required", nameof(options));

        _deploymentName = config.LLMDeploymentName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var apiKey = !string.IsNullOrWhiteSpace(config.ApiKey)
            ? config.ApiKey
            : throw new ArgumentException("AzureOpenAI:ApiKey is required", nameof(options));

        _httpClient = httpClientFactory.CreateClient("AzureOpenAI-LLM");
        _httpClient.BaseAddress = new Uri(config.Endpoint);
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

        // Log metadata only (ADR-0004)
        _logger.LogInformation(
            "Generating LLM response. PromptLength: {PromptLength}, Deployment: {Deployment}",
            prompt.Length,
            _deploymentName);

        var requestUri = $"/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";

        var requestBody = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.7,
            max_tokens = 2000
        };

        var response = await _httpClient.PostAsJsonAsync(requestUri, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        
        // Extract the response text from the chat completion response
        var choices = responseContent.GetProperty("choices").EnumerateArray().FirstOrDefault();
        if (choices.ValueKind == JsonValueKind.Undefined)
        {
            _logger.LogWarning("No choices returned from Azure OpenAI");
            return string.Empty;
        }

        var message = choices.GetProperty("message");
        var content = message.GetProperty("content").GetString() ?? string.Empty;

        _logger.LogInformation(
            "LLM response generated. ResponseLength: {ResponseLength}, Deployment: {Deployment}",
            content.Length,
            _deploymentName);

        return content;
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

        // Log metadata only (ADR-0004)
        _logger.LogInformation(
            "Generating streaming LLM response. PromptLength: {PromptLength}, Deployment: {Deployment}",
            prompt.Length,
            _deploymentName);

        var requestUri = $"/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview";

        var requestBody = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            temperature = 0.7,
            max_tokens = 2000,
            stream = true // Enable streaming
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody)
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // SSE format: "data: {json}\n\n"
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            // Skip "[DONE]" marker
            if (line == "data: [DONE]")
                break;

            // Parse JSON and extract chunk (handle exceptions without blocking yield)
            string? chunk = null;
            try
            {
                var jsonData = line.Substring(6); // Remove "data: " prefix
                if (string.IsNullOrWhiteSpace(jsonData))
                    continue;

                using var doc = JsonDocument.Parse(jsonData);
                var root = doc.RootElement;

                // Extract delta content from choices[0].delta.content
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var content) &&
                        content.ValueKind == JsonValueKind.String)
                    {
                        chunk = content.GetString();
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse SSE chunk. Line: {Line}", line);
                // Continue processing other chunks
                continue;
            }

            // Yield chunk outside of try-catch block
            if (!string.IsNullOrEmpty(chunk))
            {
                yield return chunk;
            }
        }

        _logger.LogInformation(
            "Streaming LLM response completed. Deployment: {Deployment}",
            _deploymentName);
    }
}
