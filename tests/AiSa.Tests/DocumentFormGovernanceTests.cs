using AiSa.Application.Models;
using AiSa.Host.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace AiSa.Tests;

public class DocumentFormGovernanceTests
{
    [Fact]
    public void TryParse_Internal_Succeeds()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["classification"] = "Internal",
            ["owner"] = "TeamA"
        });

        var ok = DocumentFormGovernance.TryParse(form, 3, out var ctx, out var err);

        Assert.True(ok);
        Assert.Null(err);
        Assert.NotNull(ctx);
        Assert.Equal(DataClassification.Internal, ctx!.Classification);
        Assert.Equal(3, ctx.DocumentVersion);
        Assert.Equal("TeamA", ctx.Owner);
    }

    [Fact]
    public void TryParse_Confidential_WithoutApprover_Fails()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["classification"] = "Confidential",
            ["owner"] = "TeamA",
            ["confidentialApproved"] = "true"
        });

        var ok = DocumentFormGovernance.TryParse(form, 1, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void TryParse_Confidential_WithApproval_Succeeds()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["classification"] = "Confidential",
            ["owner"] = "TeamA",
            ["confidentialApproved"] = "true",
            ["approvedBy"] = "admin-001"
        });

        var ok = DocumentFormGovernance.TryParse(form, 1, out var ctx, out var err);

        Assert.True(ok);
        Assert.Null(err);
        Assert.True(ctx!.ConfidentialApproved);
        Assert.Equal("admin-001", ctx.ApprovedBy);
        Assert.NotNull(ctx.ApprovedAt);
    }

    [Fact]
    public void TryParse_Restricted_Fails()
    {
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["classification"] = "Restricted",
            ["owner"] = "TeamA"
        });

        var ok = DocumentFormGovernance.TryParse(form, 1, out _, out var err);

        Assert.False(ok);
        Assert.Contains("Restricted", err, StringComparison.OrdinalIgnoreCase);
    }
}
