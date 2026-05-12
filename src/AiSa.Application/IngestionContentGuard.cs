using System.Text.RegularExpressions;

namespace AiSa.Application;

/// <summary>
/// Best-effort pattern guard for secrets / credential-like content at ingestion (no match text in logs).
/// </summary>
public sealed class IngestionContentGuard
{
    private static readonly Regex[] Patterns =
    {
        new(@"\bAKIA[0-9A-Z]{16}\b", RegexOptions.Compiled),
        new(@"\b(?:sk|pk)-live-[0-9a-zA-Z]{20,}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.Compiled),
        new(@"\bapi[_-]?key\s*[=:]\s*[\w\-]{8,}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    /// <summary>
    /// Returns true if content should be rejected.
    /// </summary>
    public bool ShouldReject(string content, out string reasonCode)
    {
        reasonCode = string.Empty;
        if (string.IsNullOrEmpty(content))
            return false;

        foreach (var re in Patterns)
        {
            if (re.IsMatch(content))
            {
                reasonCode = "sensitive_pattern";
                return true;
            }
        }

        return false;
    }
}
