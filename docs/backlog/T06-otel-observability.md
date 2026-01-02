# T06 - Observability: OpenTelemetry + Dashboards + Runbooks

## Goal
Make the system operable: traces, metrics, logs (safe), and actionable runbooks.

## Scope
- OpenTelemetry instrumentation:
  - request span
  - retrieval span (vector query)
  - llm span (tokens, duration)
  - tool span
- Metrics:
  - chat_requests_total
  - chat_errors_total
  - chat_latency_ms (histogram)
  - tokens_in/out
  - estimated_cost
  - retriever_precision/recall (aggregated from eval, not real-time)
  - security_events_total (tool calls blocked, unsafe requests detected)
- Portal Observability page:
  - recent request summaries (safe)
  - security events summary (blocked tool calls, unsafe agent terminations)
  - links to runbooks
- Write 2 runbooks:
  - latency incident
  - cost spike incident
- Add runbook: incident-llm-degradation.md (SLO violations, fallback strategies)

## Acceptance Criteria
- Each chat request produces correlated trace with child spans.
- Metrics counters increase correctly.
- Observability page shows recent request summaries + runbooks.

## Files / Areas
- src/AiSa.Host + Infrastructure: OTel setup
- docs/runbooks/*
- docs/quality.md (SLO check)

## DoD
- Tracing + metrics implemented
- No sensitive logs
- Runbooks referenced in UI

## Demo
1) Make 3 chat calls
2) Observability shows recent summaries + runbook links

## Sources (passive)
- OpenTelemetry .NET documentation (tracing/metrics)
- YouTube: “Observability vs monitoring” + “OpenTelemetry in .NET”
- Book (free): Google SRE concepts (SLO/SLI)

### Related context
- docs/architecture.md
- docs/runbooks/incident-latency.md
- docs/runbooks/incident-cost-spike.md
- docs/quality.md
- docs/adr/0004-telemetry-policy.md
- docs/adr/0007-finops-budgeting.md