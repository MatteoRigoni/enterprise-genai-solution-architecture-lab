namespace AiSa.Application.ToolCalling;

public sealed record ToolOutputSanitizeResult(string Text, bool WasTruncated, int RedactionCount);
