# Eval Datasets and Reports

This folder contains versioned evaluation datasets and reports used to measure the quality of the AiSa RAG experience and to prevent regressions.

## Dataset Format

- **File location**: `eval/datasets/*.json`
- **Schema** (serialized as JSON):
  - `name` – human-readable dataset name (for example, `base`)
  - `version` – semantic version string (for example, `1.0.0`)
  - `questions` – array of questions

Each question has the following fields:

- `question` – natural-language question to send to the chat endpoint
- `expectedKeyFacts` – list of key facts that should appear in a good answer
- `expectedDocIds` (optional) – expected document IDs or source names that should be cited
- `category` (optional) – logical grouping (for example, `architecture`, `eval`, `finops`)

See `datasets/base.json` for a concrete example.

## Reports Format

Eval runs produce `EvalReport` JSON files that contain:

- `datasetName` / `datasetVersion`
- `metrics` – aggregate `EvalMetrics` (answered rate, citation presence, citation accuracy, hallucination rate, latency stats)
- `results` – per-question `EvalResult` entries
- `runTimestamp` / `runDurationMs`

Reports are written under `eval/reports/` with timestamped filenames (for example, `YYYYMMDD-HHMM.json`).

## How to Extend the Dataset

1. Copy an existing dataset or append new questions to an existing one.
2. Keep questions grounded in realistic user journeys for the AiSa lab.
3. Populate `expectedKeyFacts` with succinct phrases that should appear in correct answers.
4. When possible, set `expectedDocIds` to the IDs or source names that should be cited.
5. Submit dataset changes through code review so they can be validated and versioned.

## How Evals Are Run (Overview)

- **EvalRunner console app** (CLI) runs batch evaluations against the `/api/chat` endpoint and writes reports to `eval/reports/`.
- **Portal Evaluations page** can trigger a smaller "smoke eval" in-process and display the latest report summary and metrics.

Both paths use the same underlying dataset schema and report models defined in the Domain project.

