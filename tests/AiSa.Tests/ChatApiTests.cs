using System.Net;
using System.Net.Http.Json;
using AiSa.Application;
using AiSa.Application.Models;
using AiSa.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AiSa.Tests;

/// <summary>
/// Integration tests for the /api/chat endpoint.
/// Tests the deterministic response for "hello" input as required by T01 acceptance criteria.
/// Note: With T02.D (RAG), ChatService now uses IRetrievalService. We use MockRetrievalService
/// in tests to avoid requiring Azure services configuration.
/// </summary>
public class ChatApiTests : IClassFixture<WebApplicationFactory<AiSa.Host.Program>>
{
    private readonly WebApplicationFactory<AiSa.Host.Program> _factory;
    private readonly HttpClient _client;

    public ChatApiTests(WebApplicationFactory<AiSa.Host.Program> factory)
    {
        // Configure factory to use MockRetrievalService instead of real RetrievalService
        // This avoids requiring Azure AI Search and Azure OpenAI configuration in tests
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace IRetrievalService with MockRetrievalService (returns empty results)
                // This makes ChatService return "I don't know based on provided documents."
                services.Remove(services.FirstOrDefault(s => s.ServiceType == typeof(IRetrievalService))!);
                services.AddScoped<IRetrievalService, MockRetrievalService>();
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostChat_WithHelloInput_ReturnsDeterministicResponse()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "hello"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.NotNull(chatResponse.Response);
        
        // With T02.D (RAG), ChatService now uses retrieval. Since MockRetrievalService returns empty results,
        // ChatService returns "I don't know based on provided documents." instead of the mock LLM response.
        // This is expected behavior when no documents are indexed.
        Assert.Contains("I don't know", chatResponse.Response, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(chatResponse.CorrelationId);
        Assert.NotEmpty(chatResponse.CorrelationId);
    }

    [Fact]
    public async Task PostChat_WithHelloInputCaseInsensitive_ReturnsDeterministicResponse()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "HELLO"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        
        // With T02.D (RAG), empty retrieval results in "I don't know" response
        Assert.Contains("I don't know", chatResponse.Response, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(chatResponse.CorrelationId);
    }

    [Fact]
    public async Task PostChat_WithOtherInput_ReturnsMockResponse()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "test message"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.NotNull(chatResponse.Response);
        
        // With T02.D (RAG), empty retrieval results in "I don't know" response
        Assert.Contains("I don't know", chatResponse.Response, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(chatResponse.CorrelationId);
    }

    [Fact]
    public async Task PostChat_WithEmptyMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChat_WithWhitespaceMessage_ReturnsBadRequest()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "   "
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostChat_ReturnsCorrelationIdInResponse()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "hello"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.NotNull(chatResponse.CorrelationId);
        Assert.NotEmpty(chatResponse.CorrelationId);

        // Verify correlation ID is also in response header
        var correlationIdHeader = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        Assert.NotNull(correlationIdHeader);
        Assert.Equal(chatResponse.CorrelationId, correlationIdHeader);
    }
}

/// <summary>
/// Integration tests for the /api/chat endpoint with RAG (retrieval with results).
/// Tests the RAG flow when documents are successfully retrieved.
/// </summary>
public class ChatApiTestsWithRAG : IClassFixture<WebApplicationFactory<AiSa.Host.Program>>
{
    private readonly WebApplicationFactory<AiSa.Host.Program> _factory;
    private readonly HttpClient _client;

    public ChatApiTestsWithRAG(WebApplicationFactory<AiSa.Host.Program> factory)
    {
        // Configure factory to use MockRetrievalServiceWithResults (returns mock search results)
        // This allows testing the full RAG flow: retrieval → context building → LLM call
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace IRetrievalService with MockRetrievalServiceWithResults (returns mock results)
                services.Remove(services.FirstOrDefault(s => s.ServiceType == typeof(IRetrievalService))!);
                services.AddScoped<IRetrievalService, MockRetrievalServiceWithResults>();
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostChat_WithRetrievalResults_ReturnsResponseWithCitations()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "What is AI?"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        Assert.NotNull(chatResponse.Response);
        Assert.NotEmpty(chatResponse.Response);
        
        // Verify citations are present (RAG feature)
        Assert.NotNull(chatResponse.Citations);
        Assert.NotEmpty(chatResponse.Citations);
        Assert.Equal(2, chatResponse.Citations.Count());
        
        // Verify citation metadata
        var citationsList = chatResponse.Citations.ToList();
        Assert.Contains(citationsList, c => c.SourceName == "faq.txt" && c.ChunkId == "chunk-1");
        Assert.Contains(citationsList, c => c.SourceName == "faq.txt" && c.ChunkId == "chunk-2");
        
        // Verify correlation ID
        Assert.NotNull(chatResponse.CorrelationId);
        Assert.NotEmpty(chatResponse.CorrelationId);
    }

    [Fact]
    public async Task PostChat_WithRetrievalResults_ResponseContainsContext()
    {
        // Arrange
        var request = new ChatRequest
        {
            Message = "Tell me about machine learning"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chat", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatResponse);
        
        // With RAG, the response should be generated by LLM using context from retrieved chunks
        // MockLLMClient will return a response that includes the prompt content
        // The response should contain context-related content (since LLM is called with context)
        Assert.NotNull(chatResponse.Response);
        Assert.NotEmpty(chatResponse.Response);
        
        // Verify citations are present
        Assert.NotNull(chatResponse.Citations);
        Assert.NotEmpty(chatResponse.Citations);
    }
}


