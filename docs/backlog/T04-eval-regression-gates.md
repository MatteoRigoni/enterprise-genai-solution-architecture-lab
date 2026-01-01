# T04 - Eval Harness + Versioned Dataset + Regression Gates

## Goal
Create an evaluation pipeline to measure RAG quality and prevent regressions.

## Scope
- Create eval dataset format (json):
  - question
  - expected key facts (or reference answer)
  - expected citations/doc ids (optional)
- Build EvalRunner (console .NET OR python runner):
  - runs N questions against /api/chat in batch
  - stores report in eval/reports/YYYYMMDD-HHMM.json
  - outputs summary metrics
- Add simple metrics:
  - Answered (not "I don't know") rate
  - Citation presence rate
  - Latency stats
  - Safety metrics (via eval dataset with ground truth):
    - Hallucination detection (answer contradicts ground truth)
    - Citation accuracy (cited doc actually contains answer)
- Portal Evaluations page:
  - show latest report summary + “Run smoke eval” button

## Acceptance Criteria
- Dataset in eval/datasets/base.json (start with 20, grow).
- EvalRunner produces report file + console summary.
- ADR: eval strategy and thresholds.
- UI shows last run summary.

## Files / Areas
- src/AiSa.EvalRunner or eval/runner-python
- eval/README.md + datasets + reports
- src/AiSa.Host: Evaluations page + endpoint /api/eval/run
- docs/adr: update ADR-0005 if needed
- docs/quality.md: mention dataset drift detection (compare eval reports over time)

## DoD
- EvalRunner works end-to-end
- Test for dataset parsing
- Report stored deterministically

## Demo
1) Evaluations -> click "Run smoke eval"
2) View last report summary in UI

## Sources (passive)
- YouTube: “How to evaluate RAG systems”
- Docs/blog: “LLM evaluation and regression testing”
- GitHub: OpenAI eval patterns (concepts)
