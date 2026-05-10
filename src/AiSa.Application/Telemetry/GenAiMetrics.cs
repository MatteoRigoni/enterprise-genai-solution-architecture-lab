using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AiSa.Application.Telemetry;

/// <summary>
/// OpenTelemetry metrics for GenAI operations (tokens, cost, and security events).
/// All tags are low-cardinality (model id, feature, reason).
/// </summary>
public sealed class GenAiMetrics
{
    public const string MeterName = "AiSa.GenAI";

    private readonly Counter<long> _tokensIn;
    private readonly Counter<long> _tokensOut;
    private readonly Histogram<double> _estimatedCostEur;
    private readonly Counter<long> _securityEventsTotal;

    public GenAiMetrics()
    {
        var meter = new Meter(MeterName, "1.0.0");
        _tokensIn = meter.CreateCounter<long>(
            "tokens_in",
            unit: "1",
            description: "Estimated input tokens consumed (metadata-only).");
        _tokensOut = meter.CreateCounter<long>(
            "tokens_out",
            unit: "1",
            description: "Estimated output tokens produced (metadata-only).");
        _estimatedCostEur = meter.CreateHistogram<double>(
            "estimated_cost_eur",
            unit: "EUR",
            description: "Estimated request cost in EUR (may be zero when unconfigured).");
        _securityEventsTotal = meter.CreateCounter<long>(
            "security_events_total",
            unit: "1",
            description: "Security/guardrail events (blocked tool calls, unsafe terminations, rejected inputs).");
    }

    public void RecordEstimatedUsage(string feature, string modelId, long tokensIn, long tokensOut, double estimatedCostEur)
    {
        var tags = new TagList
        {
            { "feature", feature },
            { "model", modelId }
        };

        if (tokensIn > 0) _tokensIn.Add(tokensIn, tags);
        if (tokensOut > 0) _tokensOut.Add(tokensOut, tags);
        if (estimatedCostEur > 0) _estimatedCostEur.Record(estimatedCostEur, tags);
    }

    public void RecordSecurityEvent(string reason)
    {
        var tags = new TagList
        {
            { "reason", reason }
        };
        _securityEventsTotal.Add(1, tags);
    }
}

