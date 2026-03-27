using AiSa.Application.ToolCalling;

namespace AiSa.Tests;

public class ToolCallParserTests
{
    private readonly ToolCallParser _parser = new();

    [Fact]
    public void TryParse_ValidToolCall_ReturnsProposal()
    {
        var llm =
            "Thinking...\n<tool_call>{\"name\":\"GetOrderStatus\",\"arguments\":{\"orderId\":\"123\"}}</tool_call>";

        var ok = _parser.TryParse(llm, out var proposal);

        Assert.True(ok);
        Assert.NotNull(proposal);
        Assert.Equal("GetOrderStatus", proposal!.Name);
        Assert.True(proposal.Arguments.TryGetValue("orderId", out var id));
        Assert.Equal("123", id.GetString());
    }

    [Fact]
    public void TryParse_MissingCloseTag_ReturnsFalse()
    {
        var llm = "<tool_call>{\"name\":\"GetOrderStatus\",\"arguments\":{\"orderId\":\"1\"}}";
        Assert.False(_parser.TryParse(llm, out _));
    }

    [Fact]
    public void TryParse_InvalidJson_ReturnsFalse()
    {
        Assert.False(_parser.TryParse("<tool_call>not json</tool_call>", out _));
    }

    [Fact]
    public void TryParse_NoArguments_Property_IsEmptyDict()
    {
        var llm = "<tool_call>{\"name\":\"GetOrderStatus\"}</tool_call>";
        var ok = _parser.TryParse(llm, out var proposal);
        Assert.True(ok);
        Assert.NotNull(proposal);
        Assert.Empty(proposal!.Arguments);
    }
}
