# Runbook: Cost Spike or Budget Risk

## Signals
- Sudden increase in cost per answer or daily spend vs budget
- Agent runs or tool calls spiking vs baseline
- Cache hit rate dropping or reranker suddenly enabled everywhere

## Immediate Actions (first 15 minutes)
1. Verify metrics window (last 1h vs last 24h) in FinOps dashboard.
2. Check agent vs chat split; if agent dominates, **switch to Tier 2** (constrain agent/tools) per ADR-0007.
3. Inspect top expensive prompts/tools; block misbehaving tool via allow-list if needed.
4. Enable response/retrieval cache (short TTL) if disabled.

## Stabilize and Contain
- Apply **Tier 3** if budget at risk: disable agent for non-admins, cap max output tokens.
- Reduce retrieval topK/reranking and validate citation rate in smoke eval.
- If ingestion batch triggered re-embedding, pause ingestion pipeline until cost stabilizes.

## Validate
- Run quick eval smoke (answered rate, citation rate, cost per answer) to ensure quality within acceptable range.
- Confirm spend forecast back within budget projection.

## Post-incident
- Document root cause (tool abuse, cache miss, model change).
- Update finops.md playbook if new pattern emerges.
- Add alerts if detection relied on manual observation.