# Quality & Operability

## SLO (initial)
- Chat p95 latency < 8s (dev)
- Error rate < 1%
- Cost per successful answer < threshold (see FinOps)

## Telemetry expectations
- Each /api/chat request emits a correlated trace with child spans: chat.handle -> etrieval.query -> llm.generate (and 	ool.execute when applicable).
- Metrics should be available in the dashboard:
  - chat_requests_total, chat_errors_total, chat_latency_ms
  - 	okens_in, 	okens_out, estimated_cost_eur (may be zero when pricing is unconfigured)
  - security_events_total

## Quality Metrics

### Eval Metrics (see ADR-0005)

Automated eval runs measure the following metrics against the versioned dataset (eval/datasets/base.json):

| Metric | Threshold | Notes |
|--------|-----------|-------|
| Answered rate | >= 80% | Response is non-empty and not \"I don't know\" |
| Citation presence rate | >= 70% | At least one citation returned |
| Citation accuracy rate | >= 70% | Cited source matches expectedDocIds (where provided) |
| Hallucination rate | <= 10% | Response missing expected key facts (heuristic) |
| Avg latency | <= 5 s | Mean across eval questions |
| P95 latency | <= 8 s | Aligned with chat SLO above |

Reports are written to eval/reports/YYYYMMDD-HHMM.json and viewable from the portal Evaluations page.

### CI (GitHub Actions)

The workflow .github/workflows/ci.yml runs on pull requests and pushes to main: dotnet build, dotnet test, then an eval smoke against a locally started AiSa.Host with AISA_CI_EVAL=1 (deterministic FAQ retrieval stub; no Azure or Postgres required for that step). EvalRunner uses eval/datasets/smoke.json and fails the job if --min-answered-rate or --min-citation-presence-rate is not met. JSON reports are uploaded as the eval-smoke-report artifact (metadata and aggregate metrics only in logs; see ADR-0004).


### Dataset Drift Detection

Compare consecutive eval reports to detect quality degradation over time:

- Track answered rate trend and citation accuracy trend across reports.
- **Alert threshold**: if any metric drops > 10% from the previous run's value, emit a CI warning even if the absolute threshold is still met.
- Example: answered rate 85% -> 74% across two runs triggers an alert (drop of ~13%).
- Investigation checklist: check for dataset changes, model version changes, retrieval index changes, prompt changes.

## Definition of Done per ticket
- Feature works end-to-end in portal
- Tests added/updated
- Telemetry spans present
- ADR updated if architectural change
- No sensitive logs

