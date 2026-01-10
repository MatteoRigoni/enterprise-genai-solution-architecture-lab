using AiSa.Application.Models;

namespace AiSa.Tests;

public class ChatServiceTests
{
    [Fact]
    public void ChatRequest_CanBeInstantiated_WithRequiredProperties()
    {
        // Arrange & Act
        var request = new ChatRequest
        {
            Message = "Hello"
        };

        // Assert
        Assert.NotNull(request);
        Assert.Equal("Hello", request.Message);
        Assert.Null(request.CorrelationId);
    }

    [Fact]
    public void ChatRequest_CanBeInstantiated_WithCorrelationId()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var request = new ChatRequest
        {
            Message = "Hello",
            CorrelationId = correlationId
        };

        // Assert
        Assert.NotNull(request);
        Assert.Equal("Hello", request.Message);
        Assert.Equal(correlationId, request.CorrelationId);
    }

    [Fact]
    public void ChatResponse_CanBeInstantiated_WithRequiredProperties()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var response = new ChatResponse
        {
            Response = "MOCK: Hello ...",
            CorrelationId = correlationId
        };

        // Assert
        Assert.NotNull(response);
        Assert.Equal("MOCK: Hello ...", response.Response);
        Assert.Equal(correlationId, response.CorrelationId);
    }

}

