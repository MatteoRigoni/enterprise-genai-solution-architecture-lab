using AiSa.Application;

namespace AiSa.Tests;

public class IngestionContentGuardTests
{
    private readonly IngestionContentGuard _guard = new();

    [Fact]
    public void ShouldReject_Benign_Text_Returns_False()
    {
        var reject = _guard.ShouldReject("This is a normal FAQ about product returns.", out var code);

        Assert.False(reject);
        Assert.Empty(code);
    }

    [Fact]
    public void ShouldReject_Aws_Access_Key_Pattern()
    {
        var reject = _guard.ShouldReject("key AKIAIOSFODNN7EXAMPLE in doc", out var code);

        Assert.True(reject);
        Assert.Equal("sensitive_pattern", code);
    }
}
