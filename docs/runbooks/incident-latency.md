# Runbook: Latency or Timeout Spike

## Signals
- p95 latency above SLO (>8s dev target) or elevated timeout rate
- Traces show long tool calls or slow retrieval
- Model provider latency alerts triggered

## Immediate Actions (first 15 minutes)
1. Confirm scope: chat vs agent vs eval features.
2. Check observability traces for top spans by duration (retrieval, reranker, tool call).
3. Toggle **degradation** mode: reduce topK, disable reranker temporarily, cap output tokens.
4. If tool dependency slow, disable that tool for agent and communicate limitation in portal banner.

## Stabilize and Contain
- Scale out the API replica count if infra allows; confirm autoscaler working.
- Enable response cache for repeated prompts to reduce load.
- If model provider degraded, switch to secondary/cheaper model for non-admin traffic.

## Validate
- Rerun latency smoke: p95 within target and error rate <1%.
- Run quick eval to ensure citation rate not materially degraded.

## Post-incident
- Create incident note with trace ids and mitigation steps.
- Open backlog item if root cause is missing autoscaling/timeout guard.
- Update prompt/tool timeouts or retry policies if they contributed to slowness.