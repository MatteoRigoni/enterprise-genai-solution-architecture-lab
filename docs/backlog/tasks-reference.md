# GPT Project Reference

## Roadmap

**Order:** T01 -> T02 -> T03 -> T04 -> T05 -> T06 -> T06A -> T07 -> T08 -> T09 -> T10 -> T11 -> T12 -> T13 -> T14

**Global completion rules (per ticket):**

- Demo scenario (steps in portal)
- DoD checklist completed
- Commit message referencing ticket id
- Sources list consulted (short notes)

## T01 — T01 - Blazor Portal Skeleton + Chat (Mock LLM)

### Goal
Have a working portal with navigation and a Chat page calling /api/chat, using a mock LLM.

### Scope
- Blazor portal layout with left nav menu (Chat, Agent, Documents, Evaluations, Observability, Cost, Governance, Security/Compliance, Admin).
- Chat page with message list, input, send button.
- Minimal API: POST /api/chat
- Mock LLM provider: deterministic response.

### Acceptance Criteria
- UI loads, menu navigates to pages (placeholders ok).
- Chat flow works end-to-end: user message -> API -> mock response -> UI displays.
- API returns a correlation id in response.
- Telemetry: trace span "chat.request" with duration and success/failure.
- Test: API handler returns deterministic output for input "hello".

## T02 - RAG with Azure AI Search (Citations) + Documents Page

### Goal
Implement ingestion and retrieval using Azure AI Search as vector store, and show document workflow in portal.

### Scope
- Documents page:
  - Upload documents (file) first
  - Show ingestion status list (simple)
- APIs:
  - POST /api/documents (upload)
  - GET /api/documents (list)
- RAG:
  - chunking + embeddings
  - index in Azure AI Search
  - /api/chat uses retrieval and returns citations

### Acceptance Criteria
- Upload a doc -> it becomes searchable.
- /api/chat answers with citations referencing doc chunks.
- If retrieval returns empty -> assistant replies "I don't know based on provided documents."
- Add ADR:
  - chunking strategy
  - prompt format with citations
- Telemetry spans:
  - documents.ingest
  - retrieval.query
  - llm.generate

## T03 — Add pgvector Vector Store (Portable) + Provider Toggle

### Goal
Support a second vector store (pgvector) and allow switching via config; include local dev setup.

### Scope
- Implement PgVectorVectorStore using PostgreSQL + pgvector.
- Add config setting: VectorStore:Provider = "AzureSearch" | "PgVector"
- Provide local docker compose for Postgres+pgvector.
- Add Admin page section: show which provider is active.

### Acceptance Criteria
- Same ingestion and retrieval works with pgvector.
- Switching provider does not require code changes, only config.
- Add ADR: trade-off AzureSearch vs pgvector (cost, ops, features).
- Minimal benchmark script: run 30 queries and output avg latency.

## T04 — Eval Harness + Versioned Dataset + Regression Gates

### Goal
Create an evaluation pipeline to measure RAG quality and prevent regressions.

### Scope
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
- Portal Evaluations page:
  - show latest report summary + “Run smoke eval” button

### Acceptance Criteria
- Dataset in eval/datasets/base.json (start with 20, grow).
- EvalRunner produces report file + console summary.
- ADR: eval strategy and thresholds.
- UI shows last run summary.

## T05 — Tool/Function Calling + Guardrails + Injection Tests

### Goal
Add tool calling with strict controls, and a security test suite (prompt injection).

### Scope
- Implement 2 tools (mock ok):
  1) GetOrderStatus(orderId)
  2) CreateSupportTicket(subject, details)
- Allow-list: only these tools may be called.
- Validate tool inputs (schema + length + allowed chars).
- Validate tool outputs (max length, redaction).
- Maintain injection tests:
  - 10 prompts attempting to override system, exfiltrate docs, call forbidden tools, etc.
- Update docs/security.md mapping mitigations.

### Acceptance Criteria
- Chat can invoke tools when appropriate.
- Forbidden tool calls are blocked and logged safely (no sensitive text).
- Injection test suite runs and shows pass/fail.
- Admin shows tool calling enabled/disabled.

## T06 — Observability: OpenTelemetry + Dashboards + Runbooks

### Goal
Make the system operable: traces, metrics, logs (safe), and actionable runbooks.

### Scope
- OpenTelemetry instrumentation:
  - request span
  - retrieval span (vector query)
  - llm span (tokens, duration)
  - tool span
- Metrics:
  - chat_requests_total
  - chat_errors_total
  - chat_latency_ms (histogram)
  - tokens_in/out
  - estimated_cost
- Portal Observability page:
  - recent request summaries (safe)
  - links to runbooks
- Write 2 runbooks:
  - latency incident
  - cost spike incident

### Acceptance Criteria
- Each chat request produces correlated trace with child spans.
- Metrics counters increase correctly.
- Observability page shows recent request summaries + runbooks.

## T06A — Agentic AI Orchestration (Planner + Loop + Memory) with Guardrails

### Goal
Add an agent mode that can solve multi-step requests via planning + safe tool execution.

### Scope
- Endpoint: POST /api/agent/run
- Agent loop:
  - creates a plan (steps)
  - executes step-by-step with tool calls
  - stops on success or max steps/time
- Memory:
  - short-lived session memory (summary only)
  - store only safe metadata (no raw docs, no PII)
- Portal:
  - Agent page OR toggle in Chat (prefer dedicated Agent page)
  - show plan + step trace (tool called, outcome, time)

### Acceptance Criteria
- Agent executes multi-step requests reliably.
- Plan is visible in UI.
- Agent stops safely on loops/timeouts.
- Agent eval report exists and repeatable.

## T07 — Cost Controls: Token Accounting + Caching + Budget Thresholds

### Goal
Prevent surprise bills and improve performance with caching.

### Scope
- Token accounting per request and per user session (anonymous ok).
- Caching layers:
  - retrieval cache (query -> top docs)
  - optional response cache (short TTL)
- Budget threshold:
  - config: daily_budget_eur
  - if exceeded: degrade gracefully (disable LLM calls or force "I don't know")
- Portal Cost page:
  - show daily tokens and cost estimate
  - show cache hit rate

### Acceptance Criteria
- Costs visible in portal.
- When budget exceeded, system behaves deterministically and logs alert event.
- Tests: caching returns same result; budget limit triggers.

## T08 — CI with GitHub Actions

### Goal
Automate build + tests + basic eval smoke.

### Scope
- ci.yml:
  - dotnet restore/build
  - dotnet test
  - run EvalRunner smoke on small dataset (10 qs)
  - upload eval report artifact
- Add guidance for branch protection.

### Acceptance Criteria
- CI runs on PR and main.
- Fail build if tests fail or eval smoke below threshold.
- Artifacts stored.


## T09 — Infrastructure as Code (Bicep) for Azure

### Goal
Provision required Azure resources with Bicep for dev/prod.

### Scope
- main.bicep to deploy:
  - App Service plan + Web App (Linux)
  - Application Insights / Monitor
  - Azure AI Search
  - Key Vault
  - Optional: Azure Database for PostgreSQL
- params: dev/prod
- infra/README.md: how to deploy

### Acceptance Criteria
- Deploy works via az CLI.
- Outputs include endpoints and resource ids.
- Deploy is idempotent.
- No secrets in params.


## T10 — Hardening: Identity, Key Vault, RBAC, Secure Config

### Goal
Make the deployment secure-by-default.

### Scope
- Managed identity for App Service.
- Store secrets in Key Vault:
  - LLM key (Azure OpenAI or other)
  - AI Search key (if needed)
  - Postgres connection (if applicable)
- RBAC: least privilege from App Service to Key Vault.
- Admin page: Config health check (no secret values).

### Acceptance Criteria
- App reads secrets from Key Vault at runtime.
- Missing secrets show clear health check error (without leaking).
- docs/security.md updated.


## T11 — Business Case & ROI for the GenAI System

### Goal
Translate the system into measurable business value and a defendable ROI story.

### Scope
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

### Acceptance Criteria
- A non-technical stakeholder can read and understand:
  - why AI here
  - what success looks like
  - what it costs and why it’s worth it
- Assumptions are explicit (no hand-waving).


## T12 — Data Governance & Knowledge Management (RAG-ready)

### Goal
Ensure the knowledge base is controlled, auditable, and safe to use in GenAI.

### Scope
- Data classification policy:
  - Public / Internal / Confidential / Restricted
- Ingestion rules:
  - what can be indexed
  - required metadata (owner, source, last updated, retention class)
- Retention & deletion policy:
  - how to delete vectors/chunks when source is removed
- Lineage:
  - each chunk must reference original source id + version
- Staleness:
  - define “expiry” for documents and re-ingestion strategy
- Portal Governance page:
  - show classification rules summary
  - show example metadata for ingested docs

### Acceptance Criteria
- Every ingested chunk has metadata: source, owner, classification, version/timestamp.
- There is a documented process for:
  - retiring docs
  - re-indexing updated docs
  - proving what the model saw


## T13 — Security & Compliance: GDPR + EU AI Act (practical)

### Goal
Make the system legally and ethically defensible with concrete artifacts.

### Scope
- GDPR:
  - data minimization (no raw prompts/docs in logs)
  - lawful basis (internal use vs customer use)
  - retention policy alignment (tie to T12)
  - data subject rights considerations (if applicable)
- AI Act (EU):
  - risk categorization (high-level)
  - documentation obligations (system description, intended use, limitations)
  - human oversight controls (e.g. “this is advisory, cite sources”)
- Threat model:
  - prompt injection
  - data exfiltration
  - tool abuse
  - agent loop runaway
- Incident response:
  - what to do for data leak suspicion
  - what to do for prompt injection abuse

### Acceptance Criteria
- Compliance doc references concrete system behaviors:
  - what is stored, where, for how long
  - what is NOT logged
  - what controls exist
- Threat model is actionable (not generic).


## T14 — FinOps for AI: Cost-to-Value, Budgets, and Optimization Playbook

### Goal
Move from “token tracking” to real FinOps: cost-to-value governance and decision making.

### Scope
- Define cost metrics:
  - cost per request
  - cost per successful answer (from eval)
  - cost per tool-assisted resolution
- Define budget policies:
  - daily budget
  - per-user budget
  - per-feature budget (chat vs agent)
- Define optimization levers:
  - caching strategy (from T07)
  - retrieval tuning (top-k, rerank) vs cost
  - prompt minimization
- Portal FinOps page:
  - show cost trends and cost-to-value ratios (simple table is fine)
- Create FinOps playbook:
  - what to do when cost increases
  - which knobs to turn first
  - how to measure improvement

### Acceptance Criteria
- You can answer:
  - “What does one success cost?”
  - “What happens if costs double?”
  - “How do we decide whether agent mode is worth it?”
- Portal shows at least one cost-to-value view.

