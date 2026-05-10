using System.Diagnostics.Metrics;
using AiSa.Host.Telemetry;

namespace AiSa.Tests;

public class ChatMetricsTests
{
    [Fact]
    public void RecordChatRequest_Success_IncrementsRequestCounterAndRecordsLatency()
    {
        long requestDelta = 0;
        long errorDelta = 0;
        double? lastLatency = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == ChatMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "chat_requests_total")
                requestDelta += measurement;
            else if (instrument.Name == "chat_errors_total")
                errorDelta += measurement;
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "chat_latency_ms")
                lastLatency = measurement;
        });
        listener.Start();

        var metrics = new ChatMetrics();
        metrics.RecordChatRequest("chat", ChatRequestOutcome.Success, TimeSpan.FromMilliseconds(42));

        Assert.Equal(1, requestDelta);
        Assert.Equal(0, errorDelta);
        Assert.Equal(42, lastLatency);
    }

    [Fact]
    public void RecordChatRequest_ClientError_IncrementsErrors()
    {
        long errorDelta = 0;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == ChatMetrics.MeterName)
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "chat_errors_total")
                errorDelta += measurement;
        });
        listener.Start();

        var metrics = new ChatMetrics();
        metrics.RecordChatRequest("chat_stream", ChatRequestOutcome.ClientError, TimeSpan.FromMilliseconds(1));

        Assert.Equal(1, errorDelta);
    }
}
