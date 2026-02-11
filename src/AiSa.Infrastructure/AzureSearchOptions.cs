namespace AiSa.Infrastructure;

/// <summary>
/// Configuration for Azure AI Search (used when VectorStore:Provider is AzureSearch).
/// </summary>
public class AzureSearchOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string IndexName { get; set; } = "aisa-documents";
    public string ApiKey { get; set; } = string.Empty;
}
