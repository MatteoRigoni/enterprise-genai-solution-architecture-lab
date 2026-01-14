using AiSa.Application;
using AiSa.Infrastructure;

namespace AiSa.Tests;

public class MockLLMClientTests
{
    [Fact]
    public async Task GenerateAsync_WithHelloInput_ReturnsDeterministicResponse()
    {
        // Arrange
        var client = new MockLLMClient();

        // Act
        var result = await client.GenerateAsync("hello");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("MOCK: hello", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", result);
    }

    [Fact]
    public async Task GenerateAsync_WithHelloInputCaseInsensitive_ReturnsDeterministicResponse()
    {
        // Arrange
        var client = new MockLLMClient();

        // Act
        var result1 = await client.GenerateAsync("HELLO");
        var result2 = await client.GenerateAsync("Hello");
        var result3 = await client.GenerateAsync("  hello  ");

        // Assert
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", result1);
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", result2);
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", result3);
    }

    [Fact]
    public async Task GenerateAsync_WithOtherInput_ReturnsMockResponse()
    {
        // Arrange
        var client = new MockLLMClient();

        // Act
        var result = await client.GenerateAsync("test message");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("MOCK:", result);
        Assert.Contains("test message", result);
        Assert.Contains("... This is a mock LLM response.", result);
    }

    [Fact]
    public async Task GenerateAsync_ImplementsILLMClient()
    {
        // Arrange
        ILLMClient client = new MockLLMClient();

        // Act
        var result = await client.GenerateAsync("hello");

        // Assert
        Assert.NotNull(result);
        Assert.Contains("MOCK: hello", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_WithWhitespace_TrimsInput()
    {
        // Arrange
        var client = new MockLLMClient();

        // Act
        var result = await client.GenerateAsync("   hello   ");

        // Assert
        Assert.Equal("MOCK: hello! This is a deterministic mock response for testing purposes.", result);
    }
}

