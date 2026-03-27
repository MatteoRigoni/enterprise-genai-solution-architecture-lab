namespace AiSa.Application.ToolCalling;

/// <summary>Allow-listed tool names (single source of truth for Application + Infrastructure).</summary>
public static class KnownToolNames
{
    public const string GetOrderStatus = "GetOrderStatus";
    public const string CreateSupportTicket = "CreateSupportTicket";
}
