using System.Text.Json;
using AiSa.Application.ToolCalling;

namespace AiSa.Tests;

public class ToolInputValidatorTests
{
    private readonly GetOrderStatusToolInputValidator _orderValidator = new();
    private readonly CreateSupportTicketToolInputValidator _ticketValidator = new();

    private static ToolCallProposal Proposal(string toolName, JsonElement argumentsRoot)
    {
        var args = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in argumentsRoot.EnumerateObject())
            args[p.Name] = p.Value.Clone();
        return new ToolCallProposal(toolName, args);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("ORD-001")]
    [InlineData("a_b-9")]
    public void GetOrderStatus_ValidOrderId_Ok(string orderId)
    {
        using var doc = JsonDocument.Parse($"{{\"orderId\":\"{orderId}\"}}");
        var p = Proposal(KnownToolNames.GetOrderStatus, doc.RootElement);

        var r = _orderValidator.Validate(p);

        Assert.True(r.IsValid);
    }

    [Theory]
    [InlineData("12 34")]
    [InlineData("../x")]
    [InlineData("id;drop")]
    [InlineData("")]
    public void GetOrderStatus_InvalidPattern_Fails(string orderId)
    {
        var json = JsonSerializer.Serialize(new { orderId });
        using var doc = JsonDocument.Parse(json);
        var p = Proposal(KnownToolNames.GetOrderStatus, doc.RootElement);

        var r = _orderValidator.Validate(p);

        Assert.False(r.IsValid);
        Assert.NotNull(r.UserSafeMessage);
    }

    [Fact]
    public void GetOrderStatus_TooLong_Fails()
    {
        var orderId = new string('a', ToolInputLimits.OrderIdMaxLength + 1);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { orderId }));
        var p = Proposal(KnownToolNames.GetOrderStatus, doc.RootElement);

        Assert.False(_orderValidator.Validate(p).IsValid);
    }

    [Fact]
    public void GetOrderStatus_NumberToken_Fails()
    {
        using var doc = JsonDocument.Parse("{\"orderId\":12345}");
        var p = Proposal(KnownToolNames.GetOrderStatus, doc.RootElement);

        Assert.False(_orderValidator.Validate(p).IsValid);
    }

    [Fact]
    public void CreateSupportTicket_Valid_Ok()
    {
        using var doc = JsonDocument.Parse(
            "{\"subject\":\"Login issue\",\"details\":\"Cannot reset password. Line2\"}");
        var p = Proposal(KnownToolNames.CreateSupportTicket, doc.RootElement);

        var r = _ticketValidator.Validate(p);

        Assert.True(r.IsValid);
    }

    [Fact]
    public void CreateSupportTicket_DetailsWithNewlines_Ok()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            subject = "Help",
            details = "Line1\nLine2"
        }));
        var p = Proposal(KnownToolNames.CreateSupportTicket, doc.RootElement);

        Assert.True(_ticketValidator.Validate(p).IsValid);
    }

    [Fact]
    public void CreateSupportTicket_SubjectControlChar_Fails()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            subject = "Bad\u0001title",
            details = "ok"
        }));
        var p = Proposal(KnownToolNames.CreateSupportTicket, doc.RootElement);

        Assert.False(_ticketValidator.Validate(p).IsValid);
    }

    [Fact]
    public void CreateSupportTicket_DetailsTooLong_Fails()
    {
        var details = new string('x', ToolInputLimits.DetailsMaxLength + 1);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { subject = "s", details }));
        var p = Proposal(KnownToolNames.CreateSupportTicket, doc.RootElement);

        Assert.False(_ticketValidator.Validate(p).IsValid);
    }
}
