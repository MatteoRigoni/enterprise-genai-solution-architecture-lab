using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AiSa.Host.Telemetry;

/// <summary>
/// OpenTelemetry metrics for chat HTTP endpoints. Tags are low-cardinality (endpoint + outcome only).
/// </summary>
public sealed class ChatMetrics
{
    public const string MeterName = "AiSa.Chat";

    private readonly Counter<long> _requestsTotal;
    private readonly Counter<long> _errorsTotal;
    private readonly Histogram<double> _latencyMs;

    public ChatMetrics()
    {
        var meter = new Meter(MeterName, "1.0.0");
        _requestsTotal = meter.CreateCounter<long>(
            "chat_requests_total",
            unit: "1",
            description: "Total chat API requests completed (success or error).");
        _errorsTotal = meter.CreateCounter<long>(
            "chat_errors_total",
            unit: "1",
            description: "Chat API requests that ended with client or server error.");
        _latencyMs = meter.CreateHistogram<double>(
            "chat_latency_ms",
            unit: "ms",
            description: "Duration of chat endpoint handling.");
    }

    /// <param name="endpoint">Low-cardinality route label: <c>chat</c> or <c>chat_stream</c>.</param>
    public void RecordChatRequest(string endpoint, ChatRequestOutcome outcome, TimeSpan elapsed)
    {
        var o = OutcomeToTag(outcome);
        var tags = new TagList
        {
            { "endpoint", endpoint },
            { "outcome", o }
        };
        _requestsTotal.Add(1, tags);
        if (outcome != ChatRequestOutcome.Success)
            _errorsTotal.Add(1, tags);
        _latencyMs.Record(elapsed.TotalMilliseconds, tags);
    }

    private static string OutcomeToTag(ChatRequestOutcome outcome) => outcome switch
    {
        ChatRequestOutcome.Success => "success",
        ChatRequestOutcome.ClientError => "client_error",
        ChatRequestOutcome.ServerError => "server_error",
        _ => "unknown"
    };
}

public enum ChatRequestOutcome
{
    Success,
    ClientError,
    ServerError
}
