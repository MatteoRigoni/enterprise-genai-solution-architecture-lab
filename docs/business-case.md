# Business case & ROI — GenAI portal (AiSa lab)

## Audience and purpose

This note is for **product owners, IT leadership, and finance** who must decide whether to invest in this pattern (RAG + grounded chat + optional agent/tools) versus “plain search” or manual support. It states **assumptions explicitly**; replace placeholders (marked **TBD**) with your organization’s numbers before any external commitment.

**System context:** high-level architecture is in [architecture.md](./architecture.md). **Compliance posture** (GDPR / EU AI Act framing) is in [compliance.md](./compliance.md). **Data rules** for what may be indexed are in [governance.md](./governance.md).

---

## Why GenAI here (not “just search”)

| Dimension | Classic search | This portal (RAG + LLM) |
|-----------|----------------|-------------------------|
| User intent | Keyword / link lists | Natural questions; synthesized answer |
| Grounding | User opens many docs | Answers **cite** retrieved chunks; “I don’t know” when ungrounded |
| Risk controls | ACL on documents | Same ACL + **tool allow-lists**, budgets, eval gates (see [security.md](./security.md), [ADR-0007](./adr/0007-finops-budgeting.md)) |
| Cost | Mostly infra | **Per-request** LLM + retrieval; must track **cost per successful outcome** ([finops.md](./finops.md)) |

---

## Use cases (lab scope)

These align with the [architecture](./architecture.md): chat, retrieval, optional tools/MCP, eval runner, observability.

1. **Internal knowledge assistant** — Employees ask questions over curated internal docs; answers include **citations** so readers can verify. Success = correct, cited answer in one session instead of many searches.

2. **Incident / support copilot** — Faster path from symptom to **grounded** procedure or known issue text; optional **tool calls** only through approved routes (architecture: Tool Router, MCP). Success = shorter mean time to relevant information (MTTR for *information*, not necessarily full ticket closure).

3. **Delivery confidence (eval in CI)** — Automated regression checks on a versioned Q&A dataset ([ADR-0005](./adr/0005-eval-strategy.md), [quality.md](./quality.md)). Success = release train blocked when quality drops, before users see regressions.

---

## Assumptions (fill in; no hand-waving in production)

| # | Assumption | Owner | Evidence / source |
|---|------------|-------|-------------------|
| A1 | **Annual volume** of comparable questions (e.g. support tier-1 + KB): **TBD** # | Ops / product | Tickets + search logs |
| A2 | **Minutes saved** per successful AI-assisted resolution vs baseline: **TBD** min | Process owner | Time study |
| A3 | **Fully loaded cost** per support / knowledge FTE hour: **TBD** €/h | Finance | Standard rate |
| A4 | **Adoption**: % of eligible queries using the portal after rollout: **TBD** % | Product | Usage analytics |
| A5 | **LLM + infra + ops** annual run-rate at expected load: **TBD** €/y | Engineering | [cost-model.md](./cost-model.md), telemetry + pricing table |
| A6 | **Baseline** annual cost of current process (FTE + tools + search licenses): **TBD** €/y | Finance | Budget lines |

All ROI numbers below are **illustrative** until A1–A6 are filled.

---

## KPIs (efficiency, quality, cost)

| KPI | What it measures | Technical / FinOps anchor |
|-----|------------------|----------------------------|
| **Time to credible answer** | Median time from question to answer user accepts | Latency SLO + product telemetry ([quality.md](./quality.md)) |
| **Deflection / handle time** | Fewer escalations or shorter handling when AI used first | Workflow metrics (external to repo) |
| **Citation presence / accuracy** | Grounding quality | Eval metrics & thresholds ([quality.md](./quality.md), [ADR-0005](./adr/0005-eval-strategy.md)) |
| **Error rate** | Reliability | Chat SLO ([quality.md](./quality.md)) |
| **Cost per successful answer** | Financial efficiency | `total_cost_eur / successful_answers` ([finops.md](./finops.md)); budgets & tiers [ADR-0007](./adr/0007-finops-budgeting.md) |
| **Agent premium** | Cost of agent path vs simple chat | [finops.md](./finops.md) — agent vs chat |

---

## ROI model (assumption-driven)

**Formula (annual, simplified):**

- **Value from time saved (upper bound)** ≈ `queries_per_year × adoption × minutes_saved_per_query / 60 × loaded_hourly_cost`
- **Net value (rough)** ≈ `value_from_time_saved − incremental_ai_cost − migration_risk_buffer`

Incremental AI cost should include: **LLM inference** (tokens × price), **vector store + app hosting**, **engineering/ops** for model/config changes, and **eval/CI** compute. Per-request fields tracked are summarized in [cost-model.md](./cost-model.md) and [finops.md](./finops.md).

### Illustrative ROI table (replace with your numbers)

| Line item | Year 1 (illustrative) | Notes |
|-----------|------------------------|--------|
| Baseline annual cost (current state) | € **TBD** | A6 |
| Incremental AI platform cost | € **TBD** | A5; align with [ADR-0007](./adr/0007-finops-budgeting.md) scopes |
| Gross benefit (time saved, upper bound) | € **TBD** | A1–A4 formula above |
| Risk buffer (e.g. 20–30% of gross benefit) | € **TBD** | Adoption / quality uncertainty |
| **Net benefit (indicative)** | € **TBD** | Gross − incremental − buffer |
| **Payback period** | **TBD** months | When cumulative net benefit turns positive |

**Footnotes:** Cost accounting fields (`tokens_in` / `tokens_out`, `estimated_cost_eur`, `outcome`, `feature`) match [ADR-0007](./adr/0007-finops-budgeting.md) and [finops.md](./finops.md). This document does **not** redefine budget tiers.

---

## Kill criteria (when to stop or roll back)

Trigger a **formal review** (and default to **constrain or pause** LLM features) if any of the following persist over an agreed window (e.g. two sprints or 30 days):

1. **Quality** — Eval or production proxies show sustained breach of agreed thresholds (answered rate, citation metrics, hallucination proxy) per [quality.md](./quality.md); automatic drift alerts in CI as described there.
2. **Cost per outcome** — **Cost per successful answer** or **agent premium** exceeds a pre-agreed multiple of baseline cost-per-resolution, after optimization levers in [finops.md](./finops.md) are applied.
3. **Compliance / governance** — Cannot meet [governance.md](./governance.md) classification/metadata rules, [compliance.md](./compliance.md) minimization/logging rules, or security reviews in [security.md](./security.md) (e.g. uncontrolled tool surface, budget bypass).
4. **Value** — Adoption or measured time savings stay below a **TBD** floor despite UX and change management — indicates the use case is wrong, not just the model.

Kill criteria are **governance decisions**; engineering provides the metrics.

---

## Five-minute pitch (outline)

**Italiano (demo rapido):**

1. **Problema** — Troppi passaggi tra ricerca, documenti e persone per avere una risposta **affidabile** e **verificabile**.
2. **Soluzione** — Un portale che risponde in linguaggio naturale con **citazioni** alle fonti, controlli di **budget**, e **valutazione automatica** prima del rilascio.
3. **Perché non solo ricerca** — Sintesi guidata dalla conoscenza interna, con “non so” quando manca il fondamento — vedi tabella sopra.
4. **Business case** — KPI su tempo, qualità (citazioni/eval), e **costo per risposta utile**; ROI con assunzioni esplicite.
5. **Rischio** — Criteri di **stop**: qualità, costo per esito, compliance; allineati a FinOps [ADR-0007](./adr/0007-finops-budgeting.md).

**English (one-liner):** We buy speed and consistency only if **grounding, cost-per-outcome, and compliance** stay inside pre-agreed bounds — otherwise we degrade or stop, by design.

---

## Related documents

- [cost-model.md](./cost-model.md) — per-request cost fields  
- [finops.md](./finops.md) — cost-to-value metrics, optimization order  
- [quality.md](./quality.md) — SLOs, eval thresholds, drift alerts  
- [adr/0007-finops-budgeting.md](./adr/0007-finops-budgeting.md) — budgeting and degradation  
