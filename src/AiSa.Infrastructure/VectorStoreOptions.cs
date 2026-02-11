namespace AiSa.Infrastructure;

/// <summary>
/// Configuration for vector store provider selection (ADR-0003).
/// </summary>
public class VectorStoreOptions
{
    /// <summary>
    /// Active provider: "AzureSearch" or "PgVector". Default: AzureSearch.
    /// </summary>
    public string Provider { get; set; } = "AzureSearch";
}
