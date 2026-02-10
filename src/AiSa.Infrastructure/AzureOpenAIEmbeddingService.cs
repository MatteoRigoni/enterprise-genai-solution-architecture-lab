using AiSa.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiSa.Infrastructure;

/// <summary>
/// Azure OpenAI embedding service implementation using REST API.
/// </summary>
public class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _deploymentName;
    private readonly ILogger<AzureOpenAIEmbeddingService> _logger;

    public AzureOpenAIEmbeddingService(
        IOptions<AzureOpenAIOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureOpenAIEmbeddingService> logger)
    {
        if (options?.Value == null)
            throw new ArgumentNullException(nameof(options));

        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new ArgumentException("AzureOpenAI:Endpoint is required", nameof(options));
        if (string.IsNullOrWhiteSpace(config.DeploymentName))
            throw new ArgumentException("AzureOpenAI:DeploymentName is required", nameof(options));

        _deploymentName = config.DeploymentName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var apiKey = !string.IsNullOrWhiteSpace(config.ApiKey)
            ? config.ApiKey
            : throw new ArgumentException("AzureOpenAI:ApiKey is required", nameof(options));

        _httpClient = httpClientFactory.CreateClient("AzureOpenAI");
        _httpClient.BaseAddress = new Uri(config.Endpoint);
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be null or empty", nameof(text));

        // Log metadata only (ADR-0004)
        _logger.LogInformation(
            "Generating embedding. TextLength: {TextLength}, Deployment: {Deployment}",
            text.Length,
            _deploymentName);

        var embeddings = await GenerateEmbeddingsAsync(new[] { text }, cancellationToken);
        return embeddings.First();
    }

    public async Task<IEnumerable<float[]>> GenerateEmbeddingsAsync(
        IEnumerable<string> texts,
        CancellationToken cancellationToken = default)
    {
        var textsList = texts.ToList();
        if (!textsList.Any())
            return Enumerable.Empty<float[]>();

        // Log metadata only (ADR-0004)
        _logger.LogInformation(
            "Generating embeddings in batch. TextCount: {TextCount}, Deployment: {Deployment}",
            textsList.Count,
            _deploymentName);

        var requestUri = $"/openai/deployments/{_deploymentName}/embeddings?api-version=2024-02-15-preview";

        var requestBody = new
        {
            input = textsList
        };

        var response = await _httpClient.PostAsJsonAsync(requestUri, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var dataArray = responseContent.GetProperty("data").EnumerateArray().ToList();

        var embeddings = dataArray.Select(item =>
        {
            var embeddingArray = item.GetProperty("embedding").EnumerateArray();
            return embeddingArray.Select(e => (float)e.GetDouble()).ToArray();
        }).ToList();

        _logger.LogInformation(
            "Batch embeddings generated. Count: {Count}, VectorDimension: {VectorDimension}",
            embeddings.Count,
            embeddings.FirstOrDefault()?.Length ?? 0);

        return embeddings;
    }
}

/// <summary>
/// Configuration options for Azure OpenAI.
/// </summary>
public class AzureOpenAIOptions
{
    /// <summary>
    /// Azure OpenAI service endpoint URL.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for embeddings model (e.g., text-embedding-ada-002).
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Deployment name for LLM model (e.g., gpt-4, gpt-35-turbo).
    /// </summary>
    public string LLMDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// API key for authentication (or use managed identity later).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
