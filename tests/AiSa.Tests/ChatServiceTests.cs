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
            CorrelationId = correlationId,
            MessageId = Guid.NewGuid().ToString()
        };

        // Assert
        Assert.NotNull(response);
        Assert.Equal("MOCK: Hello ...", response.Response);
        Assert.Equal(correlationId, response.CorrelationId);
    }

}

