# T04 - Eval Harness + Versioned Dataset + Regression Gates - Implementation Plan

## Task Overview
Create an evaluation pipeline to measure RAG quality and prevent regressions. Includes: versioned eval dataset, EvalRunner console app, metrics engine (answered rate, citation rate, latency, hallucination detection, citation accuracy), eval API endpoint, and portal Evaluations page.

## High-Level Plan (5 Steps)

1. **Eval data model + seed dataset** – Define domain models (EvalQuestion, EvalReport, EvalMetrics), create `eval/datasets/base.json` with 20 questions, add `eval/README.md`, and write dataset parsing test.
2. **Metrics engine + EvalRunner console** – Implement shared eval logic in Application layer (IEvalService), metric calculations, and the EvalRunner console app that calls `/api/chat` via HTTP, computes metrics, writes report to `eval/reports/YYYYMMDD-HHMM.json`.
3. **Eval API endpoint in Host** – Add `POST /api/eval/run` (smoke eval, in-process via IChatService) and `GET /api/eval/reports/latest`, storing reports on disk.
4. **Evaluations page UI** – Update `Evaluations.razor` with latest report summary, metric cards, and "Run smoke eval" button.
5. **ADR-0005 expansion + quality.md update** – Expand ADR-0005 with detailed thresholds and strategy; update `quality.md` with dataset drift detection guidance.

## Sub-Task Decomposition

### T04.A: Eval Dataset Schema + Seed Data + Parsing

**Scope:**
- Add eval domain models in `AiSa.Domain`:
  - `EvalQuestion`: question, expectedKeyFacts (string[]), expectedDocIds (string[]?, optional), category (string?, optional)
  - `EvalDataset`: name, version, questions (EvalQuestion[])
- Add eval report models in `AiSa.Domain`:
  - `EvalResult`: question, actualResponse, answered (bool), citationsPresent (bool), citationAccurate (bool?), hallucinationDetected (bool?), latencyMs (long)
  - `EvalMetrics`: answeredRate, citationPresenceRate, citationAccuracyRate, hallucinationRate, avgLatencyMs, p95LatencyMs, totalQuestions, timestamp
  - `EvalReport`: datasetName, datasetVersion, metrics (EvalMetrics), results (EvalResult[]), runTimestamp, runDurationMs
- Create `eval/datasets/base.json` – 20 questions with expected key facts and optional expected doc IDs.
- Create `eval/README.md` – documents the dataset format, how to add questions, how to run evals.
- Add dataset parsing test in `AiSa.Tests`: verify `base.json` deserializes correctly, all questions have non-empty fields.

**Files to touch:**
- `src/AiSa.Domain/Eval/EvalQuestion.cs` (new)
- `src/AiSa.Domain/Eval/EvalDataset.cs` (new)
- `src/AiSa.Domain/Eval/EvalResult.cs` (new)
- `src/AiSa.Domain/Eval/EvalMetrics.cs` (new)
- `src/AiSa.Domain/Eval/EvalReport.cs` (new)
- `eval/datasets/base.json` (new)
- `eval/README.md` (new)
- `tests/AiSa.Tests/EvalDatasetParsingTests.cs` (new)

**Minimal test:**
- `EvalDatasetParsingTests`: Deserialize `base.json`; assert 20 questions; all questions have non-empty `question` and at least one `expectedKeyFact`; dataset has name and version.

**Acceptance:**
- Models compile and are clean records/POCOs.
- `eval/datasets/base.json` has 20 well-formed questions.
- Test passes.

---

### T04.B: Eval Metrics Engine + EvalRunner Console App

**Scope:**
- Add `IEvalService` in `AiSa.Application`:
  - `ComputeMetrics(EvalResult[] results)` → `EvalMetrics`
  - Metric logic:
    - **Answered rate**: % of results where `answered == true` (response is not "I don't know" variants)
    - **Citation presence rate**: % of results where `citationsPresent == true`
    - **Citation accuracy rate**: % of results (where ground truth available) where cited doc actually matches expectedDocIds
    - **Hallucination rate**: % of results where response contradicts expected key facts (simple keyword/fact check)
    - **Latency stats**: avg, p95, min, max
- Implement `EvalRunner` console app (`src/AiSa.EvalRunner/Program.cs`):
  - Accepts arguments: `--dataset <path>` (default: `eval/datasets/base.json`), `--base-url <url>` (default: `http://localhost:5000`), `--output <dir>` (default: `eval/reports/`)
  - For each question: POST to `/api/chat`, measure latency, capture response + citations
  - Evaluate each response: answered? citations present? hallucination? citation accurate?
  - Compute aggregate metrics via `IEvalService.ComputeMetrics`
  - Write report to `eval/reports/YYYYMMDD-HHMM.json`
  - Print console summary (table of metrics)
- Add `AiSa.EvalRunner.csproj` reference to `AiSa.Application` and `AiSa.Domain`.
- Logging: metadata only (ADR-0004). No raw question text or response content in logs. Log question count, latency stats, metric values.

**Files to touch:**
- `src/AiSa.Application/Eval/IEvalService.cs` (new)
- `src/AiSa.Application/Eval/EvalService.cs` (new)
- `src/AiSa.EvalRunner/Program.cs` (replace template)
- `src/AiSa.EvalRunner/AiSa.EvalRunner.csproj` (add project refs, packages)
- `eval/reports/.gitkeep` (new, create folder)

**Minimal test:**
- Unit test `EvalMetricsTests`: Given a set of known `EvalResult[]`, `ComputeMetrics` returns correct answered rate, citation rate, latency stats.
- Manual: Run EvalRunner against a live Host; report file created; console summary printed.

**Acceptance:**
- EvalRunner runs end-to-end when Host is up.
- Report file produced in `eval/reports/YYYYMMDD-HHMM.json`.
- Console summary shows all metrics.

---

### T04.C: Eval API Endpoint in Host

**Scope:**
- Add `EvalEndpoints.cs` in `AiSa.Host/Endpoints/`:
  - `POST /api/eval/run` – runs smoke eval (first 5 questions from dataset) in-process using `IChatService` directly (no HTTP self-call). Returns `EvalReport`.
  - `GET /api/eval/reports/latest` – reads latest report from `eval/reports/` directory (by filename timestamp sort). Returns `EvalReport` or 404.
- Register `IEvalService` in DI.
- The smoke eval uses a configurable subset (default: 5 questions) to keep response time reasonable (~30-40s).
- Store smoke report in `eval/reports/` alongside EvalRunner reports.
- Telemetry: span `eval.run` with metadata (question count, duration, no raw content per ADR-0004).

**Files to touch:**
- `src/AiSa.Host/Endpoints/EvalEndpoints.cs` (new)
- `src/AiSa.Host/Program.cs` (register IEvalService, map eval endpoints)

**Minimal test:**
- Integration test: POST `/api/eval/run` returns 200 with valid EvalReport structure containing metrics.
- GET `/api/eval/reports/latest` returns the latest report.

**Acceptance:**
- Endpoints work via Swagger / curl.
- Smoke eval completes within acceptable time (~30-40s for 5 questions with mock LLM).
- Report persisted to disk.

---

### T04.D: Evaluations Page UI

**Scope:**
- Update `Evaluations.razor` to show:
  - **Hero section** with description of the eval system.
  - **Latest report summary** card: dataset name/version, timestamp, run duration.
  - **Metrics cards** (grid): answered rate, citation presence rate, citation accuracy rate, hallucination rate, avg latency, p95 latency.
  - **"Run smoke eval" button**: calls `POST /api/eval/run`, shows loading spinner, refreshes metrics on completion.
  - **Status indicators**: green/yellow/red thresholds for each metric (e.g., answered rate > 80% = green, 60-80% = yellow, < 60% = red).
  - **Error state**: if no reports exist, show "No evaluation reports yet. Run a smoke eval to get started."
- Follow existing page patterns (FluentUI components, CSS from other pages like Cost.razor or Observability.razor).

**Files to touch:**
- `src/AiSa.Host/Components/Pages/Evaluations.razor` (update)
- `src/AiSa.Host/Components/Pages/Evaluations.razor.css` (new or update)

**Minimal test:**
- Manual: Open /evaluations → see "no reports" state → click "Run smoke eval" → loading spinner → metrics appear with colored indicators.

**Acceptance:**
- UI shows last run summary.
- "Run smoke eval" button triggers eval and refreshes.
- No raw question/response text displayed (only metrics and metadata).

---

### T04.E: ADR-0005 Update + quality.md + eval/README Finalization

**Scope:**
- **Expand ADR-0005** (`docs/adr/0005-eval-strategy.md`):
  - Detail the eval strategy: versioned dataset, batch eval in CI, smoke eval from portal.
  - Define quality thresholds (initial):
    - Answered rate ≥ 80%
    - Citation presence rate ≥ 70%
    - Hallucination rate ≤ 10%
    - p95 latency ≤ 8s (aligned with quality.md SLO)
  - Document regression gate logic: CI fails if any metric below threshold.
  - Document dataset versioning policy: dataset changes require review.
- **Update quality.md** (`docs/quality.md`):
  - Add eval metrics section referencing ADR-0005.
  - Expand dataset drift detection: compare consecutive eval reports; alert if any metric drops > 10% from previous run.
- Ensure `eval/README.md` is complete with run instructions for both EvalRunner and portal.

**Files to touch:**
- `docs/adr/0005-eval-strategy.md` (update/expand)
- `docs/quality.md` (update)
- `eval/README.md` (finalize if needed)

**Minimal test:**
- Review: ADR-0005 has thresholds defined, quality.md references eval metrics, eval/README.md has complete instructions.

**Acceptance:**
- ADR-0005 is a comprehensive eval strategy document.
- quality.md updated with drift detection details.
- Documentation is self-consistent with implementation.

---

## Minimal Tests Per Sub-Task

| Sub-Task | Test Type | Test Description |
|----------|-----------|-------------------|
| T04.A | Unit | `EvalDatasetParsingTests`: base.json deserializes; 20 questions; non-empty fields |
| T04.B | Unit | `EvalMetricsTests`: ComputeMetrics returns correct rates for known inputs |
| T04.B | Manual | EvalRunner runs against live Host; report file created |
| T04.C | Integration | POST `/api/eval/run` returns valid EvalReport; GET `/api/eval/reports/latest` returns report |
| T04.D | Manual | UI shows metrics after smoke eval; "Run smoke eval" button works |
| T04.E | Review | ADR-0005 has thresholds; quality.md references eval; eval/README complete |

---

## Risks & Open Questions

### Risks
1. **Smoke eval latency** – 5 questions × ~5-8s each = 25-40s synchronous. Acceptable for demo/dev; in production, consider background job. Mitigation: use mock LLM in dev (fast), keep question count low for smoke.
2. **Hallucination detection accuracy** – Simple keyword/fact-matching is a rough heuristic, not a production-grade hallucination detector. Mitigation: document as "basic" detection; note that LLM-as-judge or human review is the gold standard (future extension).
3. **File-based report storage** – eval/reports/ is file-system based. Works for single-instance dev; not suitable for scaled production. Mitigation: document as acceptable for lab scope; note migration path to blob storage or DB.
4. **Dataset quality** – Initial 20 questions may not cover all RAG edge cases. Mitigation: document how to add questions; encourage growth over time.
5. **EvalRunner requires running Host** – Cannot run eval without a live API. Mitigation: document in README; consider health check before running.

### Open Questions
1. **Citation accuracy evaluation** – How to determine if a cited doc "actually contains the answer"? Initial approach: check if expectedDocIds overlap with returned citation sourceNames. Requires dataset questions to have expectedDocIds populated.
2. **Threshold tuning** – Initial thresholds (80% answered, 70% citation, ≤10% hallucination) are starting points. Will need calibration after first few runs.
3. **Report retention** – How many reports to keep? Initial: keep all (small files). May add retention policy later.

---

## Architecture & Doc Compliance

- **ADR-0001**: Single host – eval endpoint in AiSa.Host, consistent with single-app model.
- **ADR-0002**: Provider-agnostic LLM – EvalRunner calls `/api/chat` (provider-agnostic); Host eval uses IChatService (same abstraction).
- **ADR-0004**: No PII in logs – eval telemetry logs only metadata (question count, latency, metric values). No raw questions/responses in logs.
- **ADR-0005**: Eval strategy – this task implements the ADR's decision.
- **ADR-0007**: FinOps – eval feature tagged in cost tracking (`feature: eval`).
- **architecture.md**: EvalRunner container (line 43-47) and Evaluation Flow (line 98) already documented.
- **security.md**: No secrets in config; eval dataset must not contain restricted text (per governance.md §4 Retire/Delete).
- **quality.md**: Eval metrics and dataset drift detection to be added.
- **governance.md**: Eval datasets must not contain restricted text (§4 requirements).

---

## Branch & Commit Strategy

### Branch Name
```
feature/T04-eval-regression-gates
```

### Commit Message Pattern
- `feat(T04.A): add eval domain models, seed dataset, and parsing test`
- `feat(T04.B): implement eval metrics engine and EvalRunner console app`
- `feat(T04.C): add eval API endpoints (run smoke, get latest report)`
- `feat(T04.D): build Evaluations page UI with metrics and smoke eval button`
- `docs(T04.E): expand ADR-0005 eval strategy and update quality.md`

---

## Git Commands (DO NOT EXECUTE)

```bash
# Create branch
git checkout -b feature/T04-eval-regression-gates

# After T04.A
git add src/AiSa.Domain/Eval/ eval/datasets/base.json eval/README.md tests/AiSa.Tests/EvalDatasetParsingTests.cs
git commit -m "feat(T04.A): add eval domain models, seed dataset, and parsing test"

# After T04.B
git add src/AiSa.Application/Eval/ src/AiSa.EvalRunner/ eval/reports/.gitkeep tests/AiSa.Tests/EvalMetricsTests.cs
git commit -m "feat(T04.B): implement eval metrics engine and EvalRunner console app"

# After T04.C
git add src/AiSa.Host/Endpoints/EvalEndpoints.cs src/AiSa.Host/Program.cs
git commit -m "feat(T04.C): add eval API endpoints (run smoke, get latest report)"

# After T04.D
git add src/AiSa.Host/Components/Pages/Evaluations.razor src/AiSa.Host/Components/Pages/Evaluations.razor.css
git commit -m "feat(T04.D): build Evaluations page UI with metrics and smoke eval button"

# After T04.E
git add docs/adr/0005-eval-strategy.md docs/quality.md eval/README.md
git commit -m "docs(T04.E): expand ADR-0005 eval strategy and update quality.md"
```

---

## Next Steps

After this plan is approved:
1. Wait for "Implement T04.A" (or any sub-task).
2. Implement only the requested sub-task (~300-400 LOC max).
3. Reply with: modified files, exact verification commands, suggested git commit message.
4. Keep repo buildable; touch only files relevant to that sub-task.
