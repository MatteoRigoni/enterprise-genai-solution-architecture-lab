using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;

namespace AiSa.Application;

/// <summary>
/// Security service implementation for input validation and sanitization.
/// </summary>
public class SecurityService : ISecurityService
{
    private readonly ILogger<SecurityService> _logger;
    private readonly SecurityOptions _options;

    // Common prompt injection patterns
    private static readonly string[] PromptInjectionPatterns = new[]
    {
        @"ignore\s+(previous|all|above|prior)\s+(instructions?|commands?|directives?)",
        @"forget\s+(previous|all|above|prior)\s+(instructions?|commands?|directives?)",
        @"disregard\s+(previous|all|above|prior)\s+(instructions?|commands?|directives?)",
        @"system\s*:",
        @"###\s*(instruction|system|prompt)",
        @"you\s+are\s+(now|a|an)",
        @"act\s+as\s+(if\s+)?(you\s+are\s+)?",
        @"pretend\s+(to\s+be|you\s+are)",
        @"roleplay\s+as",
        @"new\s+instructions?",
        @"override",
        @"bypass",
        @"jailbreak"
    };

    // Dangerous characters for file names
    private static readonly char[] DangerousFileNameChars = new[]
    {
        '<', '>', ':', '"', '/', '\\', '|', '?', '*', '\0'
    };

    public SecurityService(
        IOptions<SecurityOptions> options,
        ILogger<SecurityService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public SecurityValidationResult ValidateInput(string input, int maxLength = 5000)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new SecurityValidationResult
            {
                IsValid = false,
                RejectionReason = "Input cannot be empty"
            };
        }

        var result = new SecurityValidationResult
        {
            SanitizedInput = input,
            DetectedThreats = new List<string>()
        };

        // Check length
        if (input.Length > maxLength)
        {
            result.IsValid = false;
            result.RejectionReason = $"Input exceeds maximum length of {maxLength} characters";
            result.SanitizedInput = input.Substring(0, maxLength);
            return result;
        }

        // Check for prompt injection patterns
        var detectedThreats = new List<string>();
        var normalizedInput = input.ToLowerInvariant();

        foreach (var pattern in PromptInjectionPatterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            if (regex.IsMatch(normalizedInput))
            {
                detectedThreats.Add(pattern);
            }
        }

        if (detectedThreats.Any())
        {
            result.DetectedThreats = detectedThreats;
            
            // Log threat detection (metadata only, no PII)
            _logger.LogWarning(
                "Prompt injection attempt detected. InputLength: {InputLength}, ThreatCount: {ThreatCount}, Threats: {Threats}",
                input.Length,
                detectedThreats.Count,
                string.Join(", ", detectedThreats));

            // Sanitize by removing dangerous patterns (simple approach: escape or remove)
            // For production, consider more sophisticated sanitization
            result.SanitizedInput = SanitizeInput(input, detectedThreats);
            
            // Still allow but log the threat
            result.IsValid = _options.AllowDetectedThreats;
            if (!result.IsValid)
            {
                result.RejectionReason = "Input contains potentially dangerous patterns";
            }
        }
        else
        {
            result.IsValid = true;
        }

        // Remove control characters and normalize whitespace
        result.SanitizedInput = RemoveControlCharacters(result.SanitizedInput);

        return result;
    }

    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        var sanitized = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            if (DangerousFileNameChars.Contains(c))
            {
                sanitized.Append('_');
            }
            else if (char.IsControl(c))
            {
                // Skip control characters
                continue;
            }
            else
            {
                sanitized.Append(c);
            }
        }

        var result = sanitized.ToString().Trim();
        
        // Ensure filename is not empty and doesn't end with dot or space
        if (string.IsNullOrWhiteSpace(result) || result.EndsWith('.') || result.EndsWith(' '))
        {
            result = "file";
        }

        // Limit length
        if (result.Length > 255)
        {
            result = result.Substring(0, 255);
        }

        return result;
    }

    private static string SanitizeInput(string input, IEnumerable<string> threats)
    {
        // Simple sanitization: escape common injection patterns
        // In production, consider more sophisticated approaches
        var sanitized = input;
        
        // Remove or escape dangerous patterns (simplified)
        foreach (var threat in threats)
        {
            var regex = new Regex(threat, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            sanitized = regex.Replace(sanitized, "[FILTERED]");
        }

        return sanitized;
    }

    private static string RemoveControlCharacters(string input)
    {
        return new string(input.Where(c => !char.IsControl(c) || char.IsWhiteSpace(c)).ToArray());
    }
}

/// <summary>
/// Configuration options for security service.
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// Whether to allow inputs that contain detected threats (log only) or reject them.
    /// Default: true (allow but log).
    /// </summary>
    public bool AllowDetectedThreats { get; set; } = true;

    /// <summary>
    /// Maximum input length. Default: 5000 characters.
    /// </summary>
    public int MaxInputLength { get; set; } = 5000;
}
