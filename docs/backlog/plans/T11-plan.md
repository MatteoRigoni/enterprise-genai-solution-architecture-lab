# T11 — Business Case & ROI (stakeholder narrative) — Implementation Plan

**Backlog ID:** T11 (`TD11` in commands = this task).  
**Authoritative inputs (read-only for alignment, no contradiction):** `docs/architecture.md`, `docs/security.md`, `docs/cost-model.md`, `docs/governance.md`, `docs/compliance.md`, `docs/finops.md`, ADRs `0001`–`0004`, `0006`, `0007`.

## Task overview

Deliver a **defendable business case**: 2–3 concrete use cases, KPIs, an explicit ROI model (baseline vs AI all-in cost), **kill criteria**, and a short stakeholder pitch. Primary artifact: `docs/business-case.md`. Extend `docs/quality.md` with a **business-facing SLO** (e.g. cost per successful outcome) that **points to** existing technical SLOs and FinOps metrics—without inventing new engineering thresholds that conflict with ADR-0007 or `docs/finops.md`.

**Deliverable path:** use `docs/business-case.md` (hyphen) per backlog acceptance; `docs/business_case.md` in the backlog “Related context” is a naming inconsistency—standardize on **`docs/business-case.md`**.

---

## High-level plan (6 steps)

1. **Frame** the problem, audience, and 2–3 portal-aligned use cases (plain language).
2. **Define KPIs** (efficiency, quality, cost-to-value) and map each to existing technical artifacts (eval, telemetry, FinOps).
3. **Build ROI** as a markdown table: assumptions, baseline cost, AI cost buckets, net value, payback narrative.
4. **Define kill criteria** tied to quality, cost per outcome, and compliance/governance—consistent with `docs/compliance.md` and `docs/governance.md`.
5. **Update** `docs/quality.md` with a concise “Business SLOs / value metrics” subsection with links.
6. **Self-review** against acceptance criteria (non-technical readability, no hand-waved assumptions).

---

## Sub-task decomposition

### T11.A — `docs/business-case.md` skeleton + use cases + assumptions

**Scope:**

- New file `docs/business-case.md` (1–3 pages target): title, audience, **why GenAI + RAG** (not generic search), **2–3 use cases** aligned with the lab portal (e.g. internal KB Q&A, guided incident/support workflow, eval-gated quality)—described without jargon.
- **Assumptions** section: labor rates, volumes, time-per-task today, adoption curve—explicit placeholders where unknown (label “TBD” with what evidence would replace it).
- Cross-links only to existing docs (`docs/architecture.md`, `docs/compliance.md` summary intent)—no new architecture decisions.

**Minimal tests / verification:**

- Manual: open `docs/business-case.md` in renderer; all relative links resolve.
- `dotnet build` && `dotnet test` (repo unchanged should stay green).

**Acceptance:** a stakeholder can answer “why this system?” and “what are we betting on?” from this file alone.

---

### T11.B — KPIs + ROI table + FinOps alignment

**Scope:**

- **KPI section:** e.g. time saved / deflection / faster resolution (pick metrics that match use cases); **quality** via citation reliance + eval pass rates—**reference** `docs/quality.md` and eval artifacts paths already used in the repo (no fabricated numbers).
- **ROI table (markdown):** rows for baseline annual cost, incremental AI cost (tokens/infra/ops as per `docs/cost-model.md` + `docs/finops.md`), qualitative risk offsets; **net value** and **payback** as formula + narrative, not magic numbers without assumptions.
- Explicit alignment note with **ADR-0007** (budget tiers, cost-to-value metrics already named in `docs/finops.md`)—do not introduce a competing budget model.

**Minimal tests / verification:**

- Manual: every FinOps/cost claim in the table has a footnote or assumption row pointing to `docs/cost-model.md` / `docs/finops.md` / ADR-0007.
- `dotnet build` && `dotnet test`.

**Acceptance:** finance-friendly table; assumptions visible; no contradiction with ADR-0007.

---

### T11.C — Kill criteria + `docs/quality.md` business SLO + pitch

**Scope:**

- **Kill criteria** in `docs/business-case.md`: e.g. sustained quality below threshold, cost per successful outcome above ceiling, compliance/governance gates failed—worded to mirror `docs/compliance.md` / `docs/governance.md` themes (no legal overclaim).
- **`docs/quality.md`:** add a short subsection (e.g. “Business-facing metrics / SLOs”) listing **cost per successful answer** (or equivalent) as the stakeholder view, with links to technical SLOs and FinOps definitions—**restore valid UTF-8** if the file is corrupted when opened in the editor (preserve existing technical content).
- **5-minute pitch** outline (bullets) at end of `docs/business-case.md` or under `## Demo / Pitch`.

**Minimal tests / verification:**

- Manual: non-technical reader walkthrough; kill criteria are testable concepts, not vague “AI bad.”
- `dotnet build` && `dotnet test`.
- If `docs/quality.md` was encoding-broken: verify file displays as readable Markdown.

**Acceptance:** DoD from backlog—ROI model + kill criteria + link to technical metrics; pitch ready.

---

## Risks / open questions

- **`docs/quality.md` encoding:** if the file is not valid UTF-8 text, T11.C must repair encoding while preserving intent—diff may look large; keep edits limited to that file + business-case content.
- **Numeric ROI:** lab may lack production volumes; ROI must stay **assumption-driven** and label uncertainty—avoid presenting estimates as facts.
- **Scope creep:** do not duplicate full FinOps or compliance docs; **link** instead.
- **Naming:** if another doc already claims `docs/business-case.md`, reconcile (none expected today).

---

## Branch and commits

- **Branch:** `feature/T11-business-case-roi`
- **Commit pattern:** `feat(T11): …` for substantive doc deliverables; `chore(T11): …` for typos/format-only.

---

## Git commands (do not run automatically)

```bash
git checkout main
git pull
git checkout -b feature/T11-business-case-roi
# After T11.A:
git add docs/business-case.md
git commit -m "feat(T11): add business case skeleton and use cases"
# After T11.B:
git add docs/business-case.md
git commit -m "feat(T11): add KPIs and ROI model aligned with FinOps"
# After T11.C:
git add docs/business-case.md docs/quality.md
git commit -m "feat(T11): kill criteria, business SLOs in quality doc, pitch outline"
```

---

## Execution protocol

- **Stop here** until you send: `Implement T11.A` (then only T11.A, ≤~300–400 LOC changed per sub-task).
- If any instruction in implementation would **conflict** with the authoritative docs/ADRs above, **stop** and surface the conflict before proceeding.
