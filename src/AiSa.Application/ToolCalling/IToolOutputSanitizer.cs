namespace AiSa.Application.ToolCalling;

public interface IToolOutputSanitizer
{
    /// <summary>Redacts sensitive-shaped substrings and truncates to configured max length.</summary>
    ToolOutputSanitizeResult Sanitize(string? rawOutput);
}
