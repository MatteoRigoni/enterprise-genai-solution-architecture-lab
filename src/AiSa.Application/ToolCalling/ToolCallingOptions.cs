namespace AiSa.Application.ToolCalling;

/// <summary>
/// Feature toggle and bounds for tool calling (T05).
/// </summary>
public class ToolCallingOptions
{
    public const string SectionName = "ToolCalling";

    /// <summary>When false, chat behaves as plain RAG (default).</summary>
    public bool Enabled { get; set; }

    /// <summary>Hard cap on tool executions per chat turn (defaults to 1).</summary>
    public int MaxToolCallsPerRequest { get; set; } = 1;
}
