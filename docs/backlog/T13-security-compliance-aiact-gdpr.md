# T13 - Security & Compliance: GDPR + EU AI Act (practical)

## Goal
Make the system legally and ethically defensible with concrete artifacts.

## Scope
- GDPR:
  - data minimization (no raw prompts/docs in logs)
  - lawful basis (internal use vs customer use)
  - retention policy alignment (tie to T12)
  - data subject rights (DSR) process:
    - deletion request handling (link to governance deletion process)
    - user-associated data removal procedure
    - evidence/documentation
- Data residency:
  - Azure region configuration (Key Vault, storage, compute)
  - geo-replication considerations (if applicable)
  - compliance with data localization requirements
- AI Act (EU):
  - risk categorization (high-level)
  - documentation obligations (system description, intended use, limitations)
  - human oversight controls (e.g. “this is advisory, cite sources”)
- Threat model:
  - prompt injection
  - data exfiltration
  - tool abuse
  - agent loop runaway
- Incident response (AI-specific):
  - Prompt injection abuse: detect pattern, terminate agent, log security event, review tool calls
  - Data leak suspicion: verify logs (no raw data), check tool outputs, review agent decisions, escalate
  - Cost explosion: see runbook incident-cost-spike.md
  - LLM degradation/SLO violation: see runbook incident-llm-degradation.md
  - Red teaming: periodic security testing process (document procedure, not implementation)

## Deliverables
- docs/compliance.md (GDPR + AI Act mapping in plain language)
- docs/security.md updated with a threat table:
  Threat | Impact | Mitigation | Residual risk
- Add “security assertions” into tests:
  - “no raw doc text in logs”
  - “blocked tool calls are enforced”

## Acceptance Criteria
- Compliance doc references concrete system behaviors:
  - what is stored, where, for how long
  - what is NOT logged
  - what controls exist
- Threat model is actionable (not generic).

## Files / Areas
- docs/compliance.md
- docs/security.md
- tests: basic assertions

## DoD
- Threat model table complete
- Incident response notes updated
- At least 1 automated test asserting a security invariant

## Demo
Answer to stakeholder: “How do you prevent data leakage and comply with GDPR?”

## Sources (passive)
- YouTube: “GDPR for software engineers (practical)”
- EU AI Act summaries (official/credible explainers)
- OWASP Top 10 for LLM Applications (threat categories)

### Related context
- docs/security.md
- docs/compliance.md
- docs/governance.md
- docs/architecture.md
- docs/runbooks/incident-cost-spike.md
- docs/runbooks/incident-latency.md
- docs/adr/0004-telemetry-policy.md
- docs/adr/0006-agent-loop-constraints.md