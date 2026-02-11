namespace AiSa.Application;

/// <summary>
/// Security service interface for input validation and sanitization.
/// </summary>
public interface ISecurityService
{
    /// <summary>
    /// Validates and sanitizes user input to prevent prompt injection attacks.
    /// </summary>
    /// <param name="input">User input to validate.</param>
    /// <param name="maxLength">Maximum allowed length. Default: 5000.</param>
    /// <returns>Validation result with sanitized input and detected threats.</returns>
    SecurityValidationResult ValidateInput(string input, int maxLength = 5000);

    /// <summary>
    /// Sanitizes file name to remove dangerous characters.
    /// </summary>
    /// <param name="fileName">File name to sanitize.</param>
    /// <returns>Sanitized file name.</returns>
    string SanitizeFileName(string fileName);
}

/// <summary>
/// Result of security validation.
/// </summary>
public class SecurityValidationResult
{
    /// <summary>
    /// Whether the input is safe to use.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Sanitized input (with dangerous patterns removed or escaped).
    /// </summary>
    public string SanitizedInput { get; set; } = string.Empty;

    /// <summary>
    /// List of detected threat patterns.
    /// </summary>
    public IReadOnlyList<string> DetectedThreats { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Reason for rejection if input is invalid.
    /// </summary>
    public string? RejectionReason { get; set; }
}
