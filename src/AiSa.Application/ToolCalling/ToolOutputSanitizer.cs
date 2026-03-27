using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace AiSa.Application.ToolCalling;

/// <summary>
/// Bounded tool output with pattern redaction (no logging of raw output here).
/// </summary>
public sealed class ToolOutputSanitizer : IToolOutputSanitizer
{
    private readonly int _maxLength;

    private static readonly Regex[] RedactionPatterns =
    [
        new(@"\bsk-[A-Za-z0-9]{20,}\b", RegexOptions.Compiled),
        new(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled),
        new(@"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*\b", RegexOptions.Compiled),
        new(@"\b(?:password|secret|apikey|api_key)\s*[=:]\s*\S+", RegexOptions.Compiled),
        new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled)
    ];

    public ToolOutputSanitizer(IOptions<ToolCallingOptions> options)
    {
        var n = options?.Value.MaxToolOutputCharacters ?? ToolCallingOutputLimits.DefaultMaxCharacters;
        _maxLength = n > 0 ? n : ToolCallingOutputLimits.DefaultMaxCharacters;
    }

    public ToolOutputSanitizeResult Sanitize(string? rawOutput)
    {
        if (string.IsNullOrEmpty(rawOutput))
            return new ToolOutputSanitizeResult(rawOutput ?? string.Empty, false, 0);

        var text = rawOutput;
        var redactions = 0;
        foreach (var rx in RedactionPatterns)
        {
            text = rx.Replace(text, _ =>
            {
                redactions++;
                return "[REDACTED]";
            });
        }

        var truncated = false;
        if (text.Length > _maxLength)
        {
            const string suffix = "...";
            var take = Math.Max(0, _maxLength - suffix.Length);
            text = string.Concat(text.AsSpan(0, take), suffix);
            truncated = true;
        }

        return new ToolOutputSanitizeResult(text, truncated, redactions);
    }
}
