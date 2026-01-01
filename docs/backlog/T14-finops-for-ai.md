# T14 - FinOps for AI: Cost-to-Value, Budgets, and Optimization Playbook

## Goal
Move from “token tracking” to real FinOps: cost-to-value governance and decision making.

## Scope
- Define cost metrics:
  - cost per request
  - cost per successful answer (from eval)
  - cost per tool-assisted resolution
- Define budget policies:
  - daily budget
  - per-user budget
  - per-feature budget (chat vs agent)
- Define optimization levers:
  - caching strategy (from T07)
  - retrieval tuning (top-k, rerank) vs cost
  - prompt minimization
- Portal FinOps page:
  - show cost trends and cost-to-value ratios (simple table is fine)
- Create FinOps playbook:
  - what to do when cost increases
  - which knobs to turn first
  - how to measure improvement

## Deliverables
- docs/finops.md (playbook + policies)
- Update docs/cost-model.md with cost-to-value and policy sections
- ADR: budgeting strategy and degradation rules (0007)

## Acceptance Criteria
- You can answer:
  - “What does one success cost?”
  - “What happens if costs double?”
  - “How do we decide whether agent mode is worth it?”
- Portal shows at least one cost-to-value view.

## Files / Areas
- docs/finops.md
- docs/cost-model.md
- docs/adr/0007-finops-budgeting.md
- src/AiSa.Host: FinOps page (read-only)

## DoD
- FinOps doc present
- Cost-to-value metrics computed from existing metrics
- Demo scenario documented

## Demo
Show a scenario: agent success rate goes up but cost doubles -> interpret and decide.

## Sources (passive)
- FinOps Foundation (core principles)
- YouTube: “FinOps for cloud” + “FinOps for GenAI”
- Articles: “Cost-to-serve for LLM applications”
