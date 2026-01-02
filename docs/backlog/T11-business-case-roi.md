# T11 - Business Case & ROI for the GenAI System

## Goal
Translate the system into measurable business value and a defendable ROI story.

## Scope
- Define 2–3 use cases your portal supports (e.g. internal KB assistant, incident helper, support ticket triage).
- Define KPIs:
  - time saved / ticket deflection / faster incident resolution
  - quality metric (citation rate, eval success rate)
- Build ROI model:
  - baseline cost (time * salary, current tools)
  - AI cost (LLM tokens + infra + ops time)
  - net value and payback period
- Define kill criteria:
  - if quality below threshold
  - if cost per outcome too high
  - if compliance constraints not met

## Deliverables
- docs/business-case.md (1–3 pages)
- ROI table (markdown) + assumptions section
- Update docs/quality.md with “business SLO” (e.g. cost per resolved query)

## Acceptance Criteria
- A non-technical stakeholder can read and understand:
  - why AI here
  - what success looks like
  - what it costs and why it’s worth it
- Assumptions are explicit (no hand-waving).

## Files / Areas
- docs/business-case.md
- docs/quality.md (business metric line)

## DoD
- ROI model present
- Kill criteria present
- Links to technical metrics (from eval/cost pages)

## Demo
“Pitch” di 5 minuti: perché costruire questa soluzione e non una ricerca standard.

## Sources (passive)
- YouTube: “How to calculate ROI for AI projects”
- Articles: frameworks for AI value measurement (McKinsey/Bain style, concetti)
- Book: “Lean AI” (principi di validazione e value delivery)

### Related context
- docs/business_case.md
- docs/cost-model.md
- docs/finops.md
- docs/quality.md
- docs/adr/0007-finops-budgeting.m