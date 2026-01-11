using System.Net;
using System.Net.Http.Json;
using AiSa.Application;
using AiSa.Application.Models;
using AiSa.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AiSa.Tests;

/// <summary>
/// Integration tests for the /api/chat endpoint.
/// Tests the deterministic response for "hello" input as required by T01 acceptance criteria.
/// </summary>
public class ChatApiTests : IClassFixture<WebApplicationFactory<AiSa.Host.Program>>
{
    private readonly WebApplicationFactory<AiSa.Host.Program> _factory;
    private readonly HttpClient _client;

    public ChatApiTests(WebApplicationFactory<AiSa.Host.Program> factory)
    {
        _factory = factory;
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
        Assert.Contains("MOCK: hello", chatResponse.Response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", chatResponse.Response);
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
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", chatResponse.Response);
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
        Assert.Contains("MOCK:", chatResponse.Response);
        Assert.Contains("test message", chatResponse.Response);
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


