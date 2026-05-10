namespace AiSa.Application.Eval;

/// <summary>
/// Optional CI/regression gates for aggregate eval metrics.
/// </summary>
public sealed class EvalThresholdOptions
{
    public double? MinAnsweredRate { get; init; }

    public double? MinCitationPresenceRate { get; init; }
}
