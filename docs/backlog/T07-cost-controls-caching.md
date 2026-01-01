# T07 - Cost Controls: Token Accounting + Caching + Budget Thresholds

## Goal
Prevent surprise bills and improve performance with caching.

## Scope
- Token accounting per request and per user session (anonymous ok).
- Caching layers:
  - retrieval cache (query -> top docs)
  - optional response cache (short TTL)
- Budget threshold:
  - config: daily_budget_eur
  - if exceeded: degrade gracefully (disable LLM calls or force "I don't know")
- Portal Cost page:
  - show daily tokens and cost estimate
  - show cache hit rate

## Acceptance Criteria
- Costs visible in portal.
- When budget exceeded, system behaves deterministically and logs alert event.
- Tests: caching returns same result; budget limit triggers.

## Files / Areas
- src/AiSa.Application: cost service, caching interface
- src/AiSa.Infrastructure: cache impl (MemoryCache ok)
- src/AiSa.Host: Cost page
- docs/adr/0007-finops-budgeting.md

## DoD
- Budget gating implemented
- UI displays metrics
- Runbook update for cost spike

## Demo
1) Set low budget -> spam chat -> budget triggers
2) Cost page shows spike + gating

## Sources (passive)
- YouTube: “Token cost optimization strategies”
- Docs: Azure OpenAI / LLM pricing + rate limits (concepti)
- Blog: caching patterns for RAG
