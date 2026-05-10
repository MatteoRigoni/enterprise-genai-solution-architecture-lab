namespace AiSa.Application.FinOps;

/// <summary>
/// Minimal pricing configuration used to compute per-request estimated cost (ADR-0007).
/// When prices are left as 0, cost estimation is effectively disabled (still safe to export).
/// </summary>
public sealed class FinOpsPricingOptions
{
    /// <summary>
    /// EUR per 1K input tokens.
    /// </summary>
    public double InputEurPer1KTokens { get; set; } = 0;

    /// <summary>
    /// EUR per 1K output tokens.
    /// </summary>
    public double OutputEurPer1KTokens { get; set; } = 0;

    /// <summary>
    /// Character-to-token heuristic for estimation when provider usage is unavailable.
    /// Typical rough approximation is ~4 chars/token for English text.
    /// </summary>
    public double CharsPerToken { get; set; } = 4.0;
}

