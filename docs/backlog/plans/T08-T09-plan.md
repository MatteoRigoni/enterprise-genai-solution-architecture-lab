# T08 + T09 — CI (GitHub Actions) and Azure Bicep — Implementation Plan

## Task overview

**T08** adds continuous integration: build, test, eval smoke with quality gates, artifacts, and documentation for branch protection and release notes (slots, flags, rollback).

**T09** adds infrastructure as code: Bicep templates to provision the Azure footprint described in the backlog (App Service, monitoring, AI Search, Key Vault, optional PostgreSQL), with dev/prod parameters and deployment documentation.

**Why they belong together:** CI can validate application code *and* infrastructure templates on every change (`bicep build` / lint) without deploying, keeping IaC and app drift visible early. T09 outputs (endpoints, resource IDs) are the same surface area later hardened in **T10** (managed identity, Key Vault secrets—no secrets in repo per `docs/security.md` and T09 acceptance criteria).

**Authoritative alignment:** `docs/architecture.md`, `docs/security.md`, `docs/governance.md`, `docs/compliance.md`, `docs/cost-model.md`, `docs/finops.md`; ADRs **0001** (single host), **0002** (provider-agnostic LLM), **0003** (dual vector store / config toggle), **0004** (telemetry, no PII in logs—CI must not publish prompts/reports containing raw user content in logs), **0006** (agent guardrails—out of scope for these tickets but CI gates protect regressions), **0007** (FinOps—eval/cost-related metrics stay metadata-only in CI output).

**Stop condition:** This document is planning only. Do **not** implement until the user sends `Implement T08.A` (or the specific sub-task id below).

---

## Baseline (repo today)

- **No** `.github/workflows/` yet.
- **`infra/`** is empty or absent as an IaC tree; T09 introduces `infra/bicep/*` and `infra/README.md`.
- **EvalRunner** (`src/AiSa.EvalRunner/Program.cs`) runs the **full** dataset file, always exits `0`, and has no **threshold / max-questions** flags. `eval/datasets/base.json` has **20** questions; T08 backlog asks for a **10-question** smoke—address via a dedicated `smoke.json`, CLI limits, or both.
- **Integration tests** already use `WebApplicationFactory<AiSa.Host.Program>`—useful reference for how Host boots with mock configuration in CI.

---

## High-level plan (6 steps)

1. **T08.A** — Add a minimal GitHub Actions workflow: `dotnet restore`, `dotnet build`, `dotnet test` on PR and default branch.
2. **T08.B** — Add an eval smoke job: start **AiSa.Host** (mock-friendly config), run EvalRunner against a **10-question** dataset, enforce **exit thresholds**, upload report artifact; ensure logs are metadata-only (ADR-0004).
3. **T08.C** — Document branch protection expectations, CI gates, and release/deployment strategy pointers in `eval/README.md` / `docs/quality.md` as in T08 backlog (no unrelated doc churn).
4. **T09.A** — Scaffold `infra/bicep` with a root `main.bicep` (and optional modules) for App Service Plan + Linux Web App, Log Analytics + Application Insights, Key Vault, Azure AI Search; align naming with **single-host** ADR-0001.
5. **T09.B** — Add parameterized **dev** / **prod** parameter files (no secrets committed), outputs for endpoints and resource IDs, and `infra/README.md` with `az deployment` examples.
6. **T08–T09 integration (optional but recommended)** — Add a CI job step or workflow that runs `az bicep build` (or `bicep build` CLI) on `infra/bicep` so broken IaC fails PRs without Azure credentials.

---

## Sub-task decomposition

### T08.A — CI: build + test

**Scope:** `.github/workflows/ci.yml` (or split files later): triggers on `pull_request` and `push` to `main` (adjust branch names to match repo default). Cache NuGet if appropriate. Single OS (e.g. `ubuntu-latest`) unless Windows is required.

**Minimal tests:** Green `dotnet test` in CI; existing test suite is the gate.

**Acceptance:** PR and main runs execute build + test; failures block merge when branch protection is enabled.

---

### T08.B — CI: eval smoke + artifacts + thresholds

**Scope:**

- Add **`eval/datasets/smoke.json`** (10 questions) or extend EvalRunner with `--max-questions` / `--limit` slicing the base dataset—prefer an explicit smoke file for stability.
- Extend **EvalRunner** (small, focused change): optional flags e.g. `--min-answered-rate`, `--min-citation-presence-rate` (names per `EvalMetrics`), and **non-zero exit** when below threshold; keep console summary **aggregate only** (no bulk dumping of user questions/responses into logs).
- Workflow steps: install .NET, build, start Host in background with env/config suitable for **mock LLM** and **deterministic** eval, wait for health endpoint if present (or retry loop on `/api/chat`), run EvalRunner with `--base-url`, upload `eval/reports/*.json` as artifact.

**Minimal tests:**

- Unit test(s) on threshold evaluation logic (pure function on `EvalMetrics` or report) so CI behavior is not shell-only.
- Optional: one integration test that already covers chat remains green.

**Acceptance:** CI fails when metrics fall below configured smoke thresholds; artifact contains the JSON report; T08 acceptance criteria satisfied.

---

### T08.C — Docs: gates, branch protection, release strategy

**Scope:** Short sections in `eval/README.md` (thresholds, how CI invokes smoke) and `docs/quality.md` (CI gates reference). Document **branch protection** checklist (required status checks) in a few bullets—no need for org-specific screenshots.

**Minimal tests:** N/A (documentation); optional markdown link check if repo already uses one.

**Acceptance:** New contributor can understand why CI failed and how smoke differs from full eval.

---

### T09.A — Bicep: core resources

**Scope:** `infra/bicep/main.bicep` (+ modules if needed) deploying:

- App Service Plan + Linux Web App (single host for API + Blazor per ADR-0001)
- Log Analytics + Application Insights (telemetry alignment with ADR-0004 / architecture)
- Key Vault (vault for later secret placement in T10—not populating secrets here)
- Azure AI Search service
- Optional: Azure Database for PostgreSQL Flexible Server (parameter-gated `true`/`false` for cost control in dev)

Use API versions and SKUs appropriate for a **lab** (cost-conscious defaults in dev).

**Minimal tests:**

- Local: `az bicep build --file main.bicep` succeeds.
- No automated test required if CI job from integration step validates compile.

**Acceptance:** Template compiles; resources are minimal but sufficient for the app’s configurable vector store path.

---

### T09.B — Bicep: parameters, outputs, README

**Scope:**

- `infra/bicep/parameters.dev.bicepparam` and `infra/bicep/parameters.prod.bicepparam` (or `.json` parameter files—pick one style and document it). **No secrets** in files; use placeholders and Key Vault / managed identity patterns only as *documentation* until T10.
- **Outputs:** Web App default hostname, Application Insights connection string **or** instrumentation key reference pattern (prefer connection string output only if consistent with app settings docs), Search endpoint/name, Key Vault URI, resource group–scoped IDs as needed.
- `infra/README.md`: prerequisites, `az login`, `az group create`, `az deployment group create`, idempotency note (re-run same deployment).

**Minimal tests:** Manual deploy to a disposable subscription/rg; document expected `az` commands.

**Acceptance:** Meets T09 acceptance criteria; deploy is repeatable and idempotent.

---

### T08–T09.C — CI: validate Bicep (cross-link)

**Scope:** Add job or step: install Azure CLI + Bicep, run build/validate on `infra/bicep/**/*.bicep` **without** `az deployment` (no Azure login required for `bicep build`).

**Minimal tests:** Deliberately broken Bicep fails CI (can be verified once in a throwaway branch).

**Acceptance:** T08 and T09 stay in sync—IaC syntax errors cannot merge silently.

---

## Risks and open questions

| Risk / question | Mitigation |
|-----------------|------------|
| EvalRunner today always returns exit code 0 | T08.B must add threshold exit codes or a wrapper script that parses JSON—prefer first-class CLI flags. |
| Smoke needs a running Host; port and startup time flaky in GHA | Use explicit port binding, health/retry loop, and mock LLM config env vars documented in workflow comments. |
| Mock vs real RAG in CI | Smoke should use **mock** or seeded data so CI does not depend on Azure Search; confirm current Host appsettings for CI. |
| Bicep cost if team deploys prod SKUs | Document dev SKUs; parameterize expensive options off by default. |
| **Conflict check:** If any Bicep or CI step required logging raw prompts/doc text, it would violate ADR-0004—do not add such logging; artifacts are file-based for engineers only. |

**Open questions for the user (non-blocking for T08.A):**

1. Default branch name: `main` vs `master` for workflow `on:` filters?
2. Should optional PostgreSQL in Bicep default to **off** for dev to reduce cost?
3. Is a single combined branch acceptable for the lab, or strictly separate `feature/T08-*` then `feature/T09-*` PRs?

---

## Branch names and commit message pattern

- **T08 work:** `feature/T08-github-actions-ci` (example)
- **T09 work:** `feature/T09-azure-bicep-infra` (example)
- **Cross-cutting (T08–T09.C):** either the T08 branch after T09 lands, or `feature/T08-bicep-validate-ci` if split

**Commits:**

- `feat(T08): add GitHub Actions build and test workflow`
- `feat(T08): add eval smoke job with thresholds and artifacts`
- `chore(T08): document CI gates and branch protection`
- `feat(T09): add Azure Bicep templates for core services`
- `docs(T09): add infra deployment README and parameters`
- `ci(T08): validate Bicep compilation in workflow` (or `chore(T08): ...`)

---

## Exact git commands (do not run automatically)

```bash
git fetch origin
git checkout main
git pull origin main

# T08 first slice
git checkout -b feature/T08-github-actions-ci
git add .github/workflows/ci.yml
git commit -m "feat(T08): add GitHub Actions build and test workflow"

# T09 first slice (example; after T08 merged or from main)
git checkout main
git pull origin main
git checkout -b feature/T09-azure-bicep-infra
git add infra/bicep/main.bicep
git commit -m "feat(T09): add Azure Bicep templates for core services"
```

(Adjust paths and messages to match actual sub-task deliverables.)

---

## Execution protocol (from backlog command)

1. **This plan is complete; implementation waits** for an explicit message such as `Implement T08.A`.
2. Each implementation sub-task: **≤ ~300–400 LOC** changed, **buildable** repo, **only relevant files**.
3. End each implementation message with: **modified files**, **exact verification commands**, **suggested commit message**.
