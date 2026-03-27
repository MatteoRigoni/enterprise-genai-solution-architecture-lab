using System.Text.Json;
using AiSa.Application.ToolCalling;

namespace AiSa.Tests;

public class ToolProposalArgsHasherTests
{
    private static ToolCallProposal ProposalForArgs(string argumentsObjectJson)
    {
        using var doc = JsonDocument.Parse(argumentsObjectJson);
        var root = doc.RootElement;
        var args = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in root.EnumerateObject())
            args[p.Name] = p.Value.Clone();
        return new ToolCallProposal(KnownToolNames.GetOrderStatus, args);
    }

    [Fact]
    public void SameLogicalArgs_DifferentPropertyOrder_SameHash()
    {
        var p1 = ProposalForArgs("""{"orderId":"x","extra":"y"}""");
        var p2 = ProposalForArgs("""{"extra":"y","orderId":"x"}""");

        var h1 = ToolProposalArgsHasher.ComputeSha256Hex(p1);
        var h2 = ToolProposalArgsHasher.ComputeSha256Hex(p2);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void DifferentArgs_DifferentHash()
    {
        var p1 = ProposalForArgs("""{"orderId":"a"}""");
        var p2 = ProposalForArgs("""{"orderId":"b"}""");

        Assert.NotEqual(
            ToolProposalArgsHasher.ComputeSha256Hex(p1),
            ToolProposalArgsHasher.ComputeSha256Hex(p2));
    }
}
