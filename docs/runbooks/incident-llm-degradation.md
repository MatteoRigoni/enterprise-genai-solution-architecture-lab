# Runbook: Incident — LLM degradation

## Summary
This runbook covers incidents where the LLM provider degrades (high latency, elevated errors, or quality regression).

**Telemetry policy (ADR-0004):** investigate using **metadata only** (correlation ids, durations, counts). Do not paste prompts, document text, or tool arguments into tickets or logs.

## Symptoms
- Chat p95 latency increases above SLO (see `docs/quality.md`)
- Increased 5xx from `/api/chat` or upstream LLM calls
- Spike in fallback messages (timeouts/circuit breaker)
- Visible quality drop in recent eval reports (if available)

## Quick triage (10 minutes)
1. **Confirm scope**
   - Is it all traffic or only one environment (dev/prod)?
   - Is it only one endpoint (`/api/chat` vs `/api/chat/stream`)?
2. **Check traces**
   - Find a few recent `correlationId` values (Portal Observability page) and open traces in Aspire Dashboard / Azure Monitor.
   - Look for `llm.generate` spans:
     - duration changes
     - error.type tags
     - `gen_ai.request.model` distribution
3. **Check metrics**
   - `chat_errors_total`, `chat_latency_ms`
   - `security_events_total` (should not spike unless guardrails are reacting)
   - `tokens_in`, `tokens_out`, `estimated_cost_eur` (for anomaly context; may be zero if pricing unconfigured)

## Stabilization actions
Choose the smallest safe action that restores availability.

### A) Reduce load / protect upstream
- Enable/verify circuit breaker + timeouts are in effect (`Resilience:AzureOpenAI:*`).
- If streaming is enabled, consider switching to non-streaming for stability (or vice versa) based on observed failures.
- Temporarily reduce expensive prompt paths:
  - lower retrieval `topK` (if configurable in your deployment)
  - disable optional tool calling / agent features (if enabled)

### B) Fail-safe user messaging
- Ensure the user-facing fallback is returned on upstream timeouts/circuit trips (already implemented in `ChatService`).
- Avoid retry storms: prefer bounded retries and exponential backoff.

### C) Provider mitigation
- If multiple deployments/models are available, switch to an alternate deployment.
- Verify the configured deployment name matches expected (`gen_ai.request.model` tag).

## Deep-dive diagnosis
- **Latency dominated by `llm.generate`**:
  - likely upstream/provider latency or throttling
  - verify network egress and DNS issues if self-hosted environment
- **Errors dominated by `llm.generate`**:
  - inspect upstream status codes (in HttpClient spans) and rate limits
  - validate credentials/endpoint configuration hasn’t drifted
- **Quality regression without errors**:
  - run evaluation pipeline and compare latest report deltas (answered rate, citation accuracy)
  - inspect recent changes: prompts, retrieval config, model deployment changes

## Communication + tracking
- Open an incident ticket referencing:
  - environment
  - timeframe
  - correlation ids (sample)
  - observed metrics deltas
  - mitigation steps taken

## Recovery validation (exit criteria)
- `chat_latency_ms` p95 back within SLO
- `chat_errors_total` back near baseline
- Traces show `llm.generate` spans mostly OK and within expected durations

