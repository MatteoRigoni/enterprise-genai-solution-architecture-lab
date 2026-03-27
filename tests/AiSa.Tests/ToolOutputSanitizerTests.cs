using AiSa.Application.ToolCalling;
using Microsoft.Extensions.Options;

namespace AiSa.Tests;

public class ToolOutputSanitizerTests
{
    private static ToolOutputSanitizer Create(int maxLen = 2048) =>
        new(Options.Create(new ToolCallingOptions { MaxToolOutputCharacters = maxLen }));

    [Fact]
    public void Sanitize_Truncates_AfterRedaction()
    {
        var s = Create(maxLen: 24);
        var raw = new string('n', 40);

        var r = s.Sanitize(raw);

        Assert.True(r.WasTruncated);
        Assert.Equal(24, r.Text.Length);
        Assert.EndsWith("...", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_Redacts_OpenAIStyleKey()
    {
        var s = Create();
        var r = s.Sanitize("prefix sk-abcdefghijklmnopqrstuvwxyz012345 suffix");

        Assert.Equal(1, r.RedactionCount);
        Assert.DoesNotContain("sk-abc", r.Text, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_Redacts_Email()
    {
        var s = Create();
        var r = s.Sanitize("Contact user@example.com please");

        Assert.True(r.RedactionCount >= 1);
        Assert.DoesNotContain("user@example.com", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_Redacts_AwsAccessKeyId()
    {
        var s = Create();
        var r = s.Sanitize("key AKIA0123456789ABCDEF ok");

        Assert.True(r.RedactionCount >= 1);
        Assert.DoesNotContain("AKIA0123456789ABCDEF", r.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_Empty_ReturnsEmpty()
    {
        var s = Create();
        var r = s.Sanitize("");

        Assert.Equal(string.Empty, r.Text);
        Assert.False(r.WasTruncated);
        Assert.Equal(0, r.RedactionCount);
    }
}
