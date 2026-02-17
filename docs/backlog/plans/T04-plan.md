# T04 - Eval Harness + Versioned Dataset + Regression Gates - Implementation Plan

## Task Overview
Create an evaluation pipeline to measure RAG quality and prevent regressions. Build an EvalRunner (console .NET app) that runs batch evaluations against `/api/chat`, stores versioned reports, computes safety/quality metrics, and expose results via a portal Evaluations page with a "Run smoke eval" trigger.

---

## High-Level Plan (6 Steps)

1. **Eval dataset format + base dataset** - Define the JSON schema for eval cases and create `eval/datasets/base.json` with 20 test cases covering normal Q&A, "I don't know" cases, citation checks, and adversarial/safety inputs.
2. **EvalRunner core** - Implement the .NET console runner: dataset loading, HTTP batch execution against `/api/chat`, metric computation (answered rate, citation presence, latency stats, hallucination detection, citation accuracy).
3. **Report generation + storage** - EvalRunner writes a structured JSON report to `eval/reports/YYYYMMDD-HHMM.json` with summary metrics + per-question details; console prints summary.
4. **Host eval endpoints + trigger** - Add `/api/eval/run` (POST, triggers smoke eval) and `/api/eval/latest-report` (GET, returns last report) endpoints in AiSa.Host.
5. **Evaluations page in portal** - Replace the placeholder Evaluations.razor with a page showing the latest report summary, metric cards, per-question results table, and a "Run smoke eval" button.
6. **ADR-0005 update + docs** - Expand ADR-0005 with threshold definitions, dataset versioning policy, CI gate strategy; update `docs/quality.md` with dataset drift detection guidance.

---

## Sub-Task Decomposition

### T04.A: Eval Dataset Schema + Base Dataset
**Scope:**
- Define a JSON schema for evaluation cases with fields:
  - `id` (string): unique case identifier
  - `question` (string): the user query
  - `expectedKeyFacts` (string[]): key facts the answer must contain
  - `expectedCitations` (string[], optional): expected source document names or IDs
  - `category` (string): "normal" | "no_answer" | "safety" | "citation_check"
  - `groundTruth` (string, optional): reference answer for hallucination detection
- Create `eval/datasets/base.json` with 20 cases:
  - ~10 normal Q&A (questions answerable from docs)
  - ~3 "I don't know" cases (questions NOT answerable from indexed docs)
  - ~4 citation accuracy cases (questions where specific doc must be cited)
  - ~3 safety/adversarial cases (prompt injection attempts, toxic requests)
- Create `eval/datasets/README.md` explaining the schema, categories, and how to add cases.

**Files to touch:**
- `eval/datasets/base.json` (new)
- `eval/datasets/README.md` (new)

**Minimal tests:**
- Unit test: deserialize `base.json`, assert 20 cases, all required fields present, no empty questions.

**Constraints:**
- No PII in eval dataset (ADR-0004).
- Safety cases must test injection patterns from `docs/security.md` threat model.

---

### T04.B: EvalRunner Core - Dataset Loading + Batch Execution + Metrics
**Scope:**
- Implement `src/AiSa.EvalRunner/Program.cs` as a .NET console app:
  - CLI args: `--dataset <path>` (default: `eval/datasets/base.json`), `--base-url <url>` (default: `http://localhost:5000`), `--output <dir>` (default: `eval/reports`)
  - Load and validate dataset JSON.
  - For each eval case, call `POST /api/chat` with `{ "message": question }`.
  - Capture: response body (`ChatResponse`), HTTP status, latency (ms).
  - Compute metrics:
    - **Answered rate**: % of responses NOT containing "I don't know" (for normal category).
    - **Citation presence rate**: % of responses with ≥1 citation.
    - **Latency stats**: min, max, avg, p95 across all calls.
    - **Hallucination rate**: % of cases where response contradicts `groundTruth` (simple: check if `expectedKeyFacts` are present in response).
    - **Citation accuracy**: % of citation_check cases where `expectedCitations` appear in actual citations.
  - Print summary to console.
- Add models: `EvalCase`, `EvalResult`, `EvalReport`, `EvalMetrics`.
- Add NuGet dependency: `System.Net.Http.Json` (or use built-in).

**Files to touch:**
- `src/AiSa.EvalRunner/Program.cs` (rewrite)
- `src/AiSa.EvalRunner/AiSa.EvalRunner.csproj` (add deps)
- `src/AiSa.EvalRunner/Models/EvalCase.cs` (new)
- `src/AiSa.EvalRunner/Models/EvalResult.cs` (new)
- `src/AiSa.EvalRunner/Models/EvalReport.cs` (new)
- `src/AiSa.EvalRunner/Models/EvalMetrics.cs` (new)
- `src/AiSa.EvalRunner/EvalRunner.cs` (new - core runner logic)

**Minimal tests:**
- Unit test: `EvalRunner` with mocked HTTP client; verify metrics computation for known inputs (e.g., 2 answered + 1 "I don't know" = 66.7% answered rate).
- Unit test: dataset deserialization handles missing optional fields gracefully.

**Constraints:**
- Telemetry: log only metadata (case ID, latency, status), never raw question/response content (ADR-0004).
- Cost: eval runs are tagged `feature=eval` for FinOps tracking (ADR-0007).
- Max 300-400 LOC changed.

---

### T04.C: Report Generation + Deterministic Storage
**Scope:**
- After metrics are computed, EvalRunner serializes a full `EvalReport` to JSON.
- Report file path: `eval/reports/YYYYMMDD-HHMM.json` (timestamp of run start).
- Report structure:
  ```json
  {
    "runId": "uuid",
    "timestamp": "ISO8601",
    "datasetPath": "eval/datasets/base.json",
    "datasetCaseCount": 20,
    "metrics": {
      "answeredRate": 0.85,
      "citationPresenceRate": 0.90,
      "citationAccuracyRate": 0.75,
      "hallucinationRate": 0.05,
      "latency": { "minMs": 120, "maxMs": 3500, "avgMs": 800, "p95Ms": 2200 }
    },
    "results": [
      {
        "caseId": "...",
        "question": "...",
        "answered": true,
        "hasCitations": true,
        "citationAccurate": true,
        "hallucinationDetected": false,
        "latencyMs": 450,
        "httpStatus": 200
      }
    ]
  }
  ```
- Console output: formatted summary table of metrics.
- Ensure `eval/reports/` directory is created if missing.
- Add `eval/reports/.gitkeep` (reports themselves are gitignored to avoid bloat).
- Add `.gitignore` entry for `eval/reports/*.json`.

**Files to touch:**
- `src/AiSa.EvalRunner/ReportWriter.cs` (new)
- `eval/reports/.gitkeep` (new)
- `.gitignore` (add `eval/reports/*.json`)

**Minimal tests:**
- Unit test: `ReportWriter` produces valid JSON with expected structure.
- Unit test: file naming follows `YYYYMMDD-HHMM.json` pattern.

**Constraints:**
- No raw user prompts/answers in logs (report file contains them for debugging but logs don't).
- Reports are local artifacts, not committed to git.

---

### T04.D: Host Eval Endpoints
**Scope:**
- Add `EvalEndpoints.cs` in `src/AiSa.Host/Endpoints/`:
  - `POST /api/eval/run` - Triggers a smoke eval run (in-process, synchronous for now; runs a subset of dataset).
    - Reads `eval/datasets/base.json` (configurable path).
    - Runs eval cases against own `/api/chat` endpoint via internal HTTP call (or direct service call via `IChatService`).
    - Returns the generated `EvalReport` JSON.
    - Rate limit: 1 run per minute (prevent abuse).
  - `GET /api/eval/latest-report` - Reads the most recent report from `eval/reports/` directory and returns it.
  - `GET /api/eval/reports` - Lists available report files (name + timestamp).
- Register endpoints in `Program.cs`.
- Add `Eval` configuration section for dataset path.

**Files to touch:**
- `src/AiSa.Host/Endpoints/EvalEndpoints.cs` (new)
- `src/AiSa.Host/Program.cs` (register eval endpoints)
- `src/AiSa.Host/appsettings.json` (add `Eval` section with dataset path)

**Minimal tests:**
- Integration test: `POST /api/eval/run` returns 200 with valid report structure.
- Unit test: `GET /api/eval/latest-report` returns most recent file from reports dir.

**Constraints:**
- The eval endpoint calls `IChatService` directly (not HTTP loopback) to avoid self-referencing issues and rate limit conflicts.
- Eval runs are tagged `feature=eval` in telemetry (ADR-0004/0007).
- No raw question/response in telemetry; report file is the detailed artifact.

---

### T04.E: Evaluations Portal Page
**Scope:**
- Replace the placeholder `Evaluations.razor` with a functional page:
  - **Latest report summary**: metric cards (answered rate, citation rate, hallucination rate, latency p95) with color coding (green/yellow/red based on thresholds).
  - **"Run smoke eval" button**: calls `POST /api/eval/run`, shows loading spinner, displays results on completion.
  - **Per-question results table**: expandable table showing each eval case result (case ID, question, answered, citations, latency, pass/fail).
  - **Report history**: dropdown or list of previous reports (from `GET /api/eval/reports`).
- Follow existing UI patterns (FluentUI components, CSS from `wwwroot/`, `ui-styleguide.md`).

**Files to touch:**
- `src/AiSa.Host/Components/Pages/Evaluations.razor` (rewrite)

**Minimal tests:**
- Manual: page loads, shows "no report yet" when no reports exist.
- Manual: click "Run smoke eval", see loading state, then results appear.
- Manual: metric cards show correct values matching the report JSON.

**Constraints:**
- UI must handle: no reports yet, eval in progress, eval completed, eval failed.
- Follow existing Blazor patterns from other pages (Chat.razor, Cost.razor).

---

### T04.F: ADR-0005 Update + Documentation
**Scope:**
- Expand `docs/adr/0005-eval-strategy.md`:
  - Add threshold definitions (e.g., answered rate ≥ 80%, citation rate ≥ 70%, hallucination rate ≤ 10%).
  - Define CI gate policy: eval must pass thresholds on main branch merges.
  - Dataset versioning: dataset changes require review; dataset is committed to git.
  - Report storage: local files, not committed; CI publishes to artifact store.
- Update `docs/quality.md`:
  - Add section on dataset drift detection: compare consecutive reports, alert if answered rate drops > 5% or hallucination rate rises > 5%.
  - Reference eval pipeline and report format.
- Update `eval/_README.md` (rename to `eval/README.md`):
  - Describe the eval infrastructure: datasets, runner, reports, CI integration.

**Files to touch:**
- `docs/adr/0005-eval-strategy.md` (expand)
- `docs/quality.md` (add drift detection section)
- `eval/README.md` (rewrite from placeholder `eval/_README.md`)

**Minimal tests:**
- Review: ADR has Status, Context, Decision, Consequences sections.
- Review: quality.md references eval reports and drift thresholds.

**Constraints:**
- ADR thresholds must align with `docs/quality.md` SLOs.
- No conflicts with existing ADRs (0004 telemetry, 0006 agent bounds, 0007 finops).

---

## Minimal Tests Per Sub-Task

| Sub-Task | Test Type | Test Description |
|----------|-----------|-------------------|
| T04.A | Unit | Deserialize `base.json`: 20 cases, all required fields present, categories correct |
| T04.B | Unit | EvalRunner metrics computation with mocked HTTP: answered rate, citation rate, latency stats correct for known inputs |
| T04.B | Unit | Dataset deserialization handles optional fields (`expectedCitations`, `groundTruth`) |
| T04.C | Unit | ReportWriter produces valid JSON matching expected schema |
| T04.C | Unit | Report filename follows YYYYMMDD-HHMM.json pattern |
| T04.D | Integration | `POST /api/eval/run` returns 200 with valid EvalReport |
| T04.D | Unit | Latest-report endpoint returns most recent report file |
| T04.E | Manual | Evaluations page loads; "Run smoke eval" works; metrics display correctly |
| T04.F | Review | ADR-0005 has thresholds; quality.md has drift detection section |

---

## Risks & Open Questions

### Risks
1. **Eval endpoint latency** - Running 20 questions synchronously against `/api/chat` could take 30-60s with a real LLM. **Mitigation**: Use `IChatService` directly (skip HTTP overhead); with MockLLM in dev, latency is negligible. Future: async/background job for production.
2. **MockLLM determinism** - MockLLM responses may not match expected key facts in eval cases. **Mitigation**: Design eval cases that work with MockLLM behavior (mock returns predictable responses) OR add a flag to skip fact-checking when using mock.
3. **Dataset maintenance burden** - 20 cases need to be kept current as docs change. **Mitigation**: Start small; dataset drift detection alerts (T04.F) will flag staleness.
4. **Self-referencing in Host** - `POST /api/eval/run` triggering `/api/chat` internally could cause issues. **Mitigation**: Call `IChatService` directly instead of HTTP loopback.
5. **Report storage growth** - Reports accumulate on disk. **Mitigation**: Gitignore reports; document cleanup policy; future: retention limit (keep last N reports).

### Open Questions
1. **Hallucination detection depth** - Current plan uses simple keyword matching (expectedKeyFacts present in response). More sophisticated approaches (semantic similarity, LLM-as-judge) can be added later. Is simple keyword matching sufficient for v1?
2. **CI gate enforcement** - ADR-0005 mentions CI gates. Should T04 include a GitHub Actions workflow, or just document the gate policy for a future task?
3. **Eval dataset source** - The 20 cases need realistic questions. Should they reference docs already ingested via T02, or be standalone (work even with empty vector store)?

**Recommended answers** (for planning purposes):
1. Simple keyword matching is sufficient for v1. Flag for future improvement.
2. Document only; CI workflow is a separate task.
3. Design cases that work with MockLLM (standalone); add a note that cases should be updated when real docs are ingested.

---

## Branch & Commit Strategy

### Branch Name
```
feature/T04A-eval-dataset
feature/T04B-eval-runner-core
feature/T04C-eval-report-storage
feature/T04D-eval-host-endpoints
feature/T04E-eval-portal-page
feature/T04F-eval-adr-docs
```
(Use single feature branch per sub-task for tight scope, or a single `feature/T04-eval-harness` if preferred.)

### Commit Message Pattern
- `feat(T04.A): add eval dataset schema and base.json with 20 test cases`
- `feat(T04.B): implement EvalRunner core with batch execution and metrics`
- `feat(T04.C): add report generation and deterministic file storage`
- `feat(T04.D): add /api/eval/run and /api/eval/latest-report endpoints`
- `feat(T04.E): implement Evaluations portal page with report summary and smoke eval`
- `docs(T04.F): expand ADR-0005 thresholds and quality.md drift detection`

---

## Git Commands (DO NOT EXECUTE)

```bash
# Create branch (single branch for all T04 sub-tasks)
git checkout -b feature/T04-eval-harness

# After T04.A
git add eval/datasets/base.json eval/datasets/README.md
git commit -m "feat(T04.A): add eval dataset schema and base.json with 20 test cases"

# After T04.B
git add src/AiSa.EvalRunner/
git commit -m "feat(T04.B): implement EvalRunner core with batch execution and metrics"

# After T04.C
git add src/AiSa.EvalRunner/ReportWriter.cs eval/reports/.gitkeep .gitignore
git commit -m "feat(T04.C): add report generation and deterministic file storage"

# After T04.D
git add src/AiSa.Host/Endpoints/EvalEndpoints.cs src/AiSa.Host/Program.cs src/AiSa.Host/appsettings.json
git commit -m "feat(T04.D): add /api/eval/run and /api/eval/latest-report endpoints"

# After T04.E
git add src/AiSa.Host/Components/Pages/Evaluations.razor
git commit -m "feat(T04.E): implement Evaluations portal page with report summary and smoke eval"

# After T04.F
git add docs/adr/0005-eval-strategy.md docs/quality.md eval/README.md
git commit -m "docs(T04.F): expand ADR-0005 thresholds and quality.md drift detection"
```

---

## Architecture & Doc Compliance Check

| Reference | Requirement | How T04 Satisfies |
|-----------|-------------|-------------------|
| docs/architecture.md (L2 EvalRunner) | Console .NET app, File I/O + HTTP, CI/CD pipeline | EvalRunner is .NET console; reads dataset files, calls API, writes reports |
| docs/architecture.md (Eval Flow) | CI/CD → EvalRunner → Dataset → API → Metrics → Report → CI gate | Exactly this flow |
| ADR-0005 | Versioned eval dataset, batch eval in CI | Dataset in git, runner produces reports, CI gate documented |
| ADR-0004 | No PII in logs | Eval telemetry uses metadata only; no raw Q&A in logs |
| ADR-0007 | Cost tracking per feature | Eval runs tagged `feature=eval` |
| docs/security.md | Prompt injection tests maintained | Safety category in dataset includes injection test cases |
| docs/quality.md | Dataset drift detection, eval metrics | Drift detection in quality.md; metrics match spec |
| docs/governance.md | Eval datasets no restricted text | Dataset uses synthetic questions only |
| ADR-0001 | Single host (API + Blazor) | Eval endpoints added to AiSa.Host |

No conflicts detected with authoritative ADRs or docs.

---

## Demo (Acceptance)

1. Navigate to **Evaluations** page in portal.
2. Click **"Run smoke eval"** button.
3. See loading indicator while eval runs.
4. View report summary: answered rate, citation rate, hallucination rate, latency stats.
5. Expand per-question results to see individual case outcomes.
6. Verify report file exists in `eval/reports/YYYYMMDD-HHMM.json`.

---

## Next Steps

After this plan is approved:
1. Wait for "Implement T04.A" (or any specific sub-task).
2. Implement only the requested sub-task (~300-400 LOC max).
3. Reply with: modified files, exact verification commands, suggested git commit message.
4. Keep repo buildable; touch only files relevant to that sub-task.
