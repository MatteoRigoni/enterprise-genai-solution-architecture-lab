using System.Collections.Concurrent;
using AiSa.Application.Observability;

namespace AiSa.Host.Services;

/// <summary>
/// In-memory bounded store for security/guardrail events (metadata only).
/// Intended for local demo and portal observability page.
/// </summary>
public sealed class InMemorySecurityEventRecorder : ISecurityEventRecorder
{
    private const int MaxRecords = 200;
    private readonly ConcurrentQueue<SecurityEventRecord> _records = new();

    public void Record(SecurityEventRecord record)
    {
        _records.Enqueue(record);
        while (_records.Count > MaxRecords && _records.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<SecurityEventRecord> GetRecent(int count = 20)
    {
        if (count <= 0) return Array.Empty<SecurityEventRecord>();
        return _records.Reverse().Take(count).ToList();
    }
}

