namespace AiSa.Application.ToolCalling;

/// <summary>
/// Shared markers for tool-calling prompts so host and LLM client stay aligned.
/// </summary>
public static class ToolCallingPrompt
{
    /// <summary>First line of the allow-list section appended by <see cref="ChatService"/>.</summary>
    public const string AllowedToolsSectionHeader = "Allowed tools (only these may be used):";
}
