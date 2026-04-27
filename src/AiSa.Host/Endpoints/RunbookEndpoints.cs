namespace AiSa.Host.Endpoints;

/// <summary>
/// Serves runbook markdown content for the portal UI.
/// This is allowlist-based to avoid path traversal or accidental exposure.
/// </summary>
internal static class RunbookEndpoints
{
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["incident-latency"] = "incident-latency.md",
        ["incident-cost-spike"] = "incident-cost-spike.md",
        ["incident-llm-degradation"] = "incident-llm-degradation.md"
    };

    public static IEndpointRouteBuilder MapRunbookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/runbooks/{name}", async (string name) =>
            {
                if (!Allowed.TryGetValue(name, out var fileName))
                {
                    return Results.NotFound();
                }

                var baseDir = AppContext.BaseDirectory;
                // baseDir: .../src/AiSa.Host/bin/{Configuration}/{TFM}/
                // repo root is 5 levels up from baseDir
                var contentRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
                var docsPath = Path.Combine(contentRoot, "docs", "runbooks", fileName);

                if (!File.Exists(docsPath))
                {
                    return Results.NotFound();
                }

                var content = await File.ReadAllTextAsync(docsPath);
                return Results.Text(content, "text/markdown; charset=utf-8");
            })
            .WithName("RunbookGet")
            .WithSummary("Get runbook markdown")
            .WithTags("Runbooks");

        return app;
    }
}

