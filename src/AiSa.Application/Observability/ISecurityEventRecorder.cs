namespace AiSa.Application.Observability;

public interface ISecurityEventRecorder
{
    void Record(SecurityEventRecord record);
}

public sealed record SecurityEventRecord(
    DateTimeOffset Timestamp,
    string Reason,
    string? CorrelationId);

