# T03 - Add pgvector Vector Store (Portable) + Provider Toggle - Implementation Plan

## Task Overview
Support a second vector store (pgvector) and allow switching via config; include local dev setup. Same ingestion and retrieval behavior; provider selection by config only; Admin page shows active provider; ADR documents trade-offs; minimal benchmark for latency.

## High-Level Plan (6 Steps)

1. **Config + provider toggle** – Add `VectorStore:Provider` ("AzureSearch" | "PgVector") and wire Host to register the chosen IVectorStore implementation based on config.
2. **PgVectorVectorStore implementation** – Implement `PgVectorVectorStore` in Infrastructure using PostgreSQL + pgvector (Npgsql), with table/index creation and full IVectorStore contract.
3. **.NET Aspire (AppHost) + README** – Add Postgres container with pgvector image in the existing AiSa.AppHost; pass connection string to AiSa.Host via Aspire reference; document how to run pgvector locally with Aspire; no secrets in config.
4. **Admin page: active provider** – Add Admin page section (or panel) that shows which vector store provider is active.
5. **ADR: AzureSearch vs pgvector** – New ADR documenting trade-offs (cost, ops, features).
6. **Minimal benchmark script** – Script that runs 30 queries and outputs average latency.

## Sub-Task Decomposition

### T03.A: Config + Provider Toggle
**Scope:**
- Add config section `VectorStore` with `Provider` = `"AzureSearch"` | `"PgVector"` (default: `AzureSearch`).
- Add `PgVector` config subsection: `ConnectionString` (or host/port/database/user; password via env only – no secrets in config per security.md).
- In Host, register IVectorStore conditionally:
  - If Provider == AzureSearch: register `AzureSearchVectorStore` (current behavior).
  - If Provider == PgVector: register `PgVectorVectorStore` with `PgVectorOptions`.
- Ensure only one implementation is registered; no code paths that switch at runtime (config-only switch at startup).

**Files to touch:**
- `src/AiSa.Host/appsettings.json` (add `VectorStore:Provider`, optional `VectorStore:PgVector`)
- `src/AiSa.Host/appsettings.Development.json` (optional overrides)
- `src/AiSa.Host/Program.cs` (conditional registration of IVectorStore)
- `src/AiSa.Infrastructure/PgVectorOptions.cs` (new, or in same file as PgVectorVectorStore) – connection string / host, port, database, user; no password in options (from env)

**Minimal test:**
- Unit or integration: When `VectorStore:Provider` = `PgVector` and PgVector config valid, resolved `IVectorStore` is `PgVectorVectorStore`; when `AzureSearch`, resolved is `AzureSearchVectorStore`.
- Build and run with both provider values; no startup exception.

**Acceptance:**
- Switching provider does not require code changes, only config.
- No secrets in config files (password via environment variable).

---

### T03.B: PgVectorVectorStore Implementation
**Scope:**
- Implement `PgVectorVectorStore` in `AiSa.Infrastructure` implementing `IVectorStore`:
  - `AddDocumentsAsync(IEnumerable<DocumentChunk>)` – insert/upsert chunks into a single table with vector column (pgvector type, 1536 dimensions).
  - `SearchAsync(float[] queryVector, int topK)` – vector similarity search (cosine or inner product to align with Azure default).
  - `DeleteBySourceIdAsync(string sourceId)` – delete rows where source_id = sourceId.
- Table schema: id (PK), chunk_id, chunk_index, content, embedding (vector(1536)), source_id, source_name, indexed_at. Create table and index (e.g. HNSW or IVFFlat) on first use if not exists.
- Use Npgsql with pgvector extension (NuGet: Npgsql, pgvector).
- Timeouts: align with existing (e.g. search 10s, index 30s) via options or cancellation.
- Logging: metadata only (ADR-0004): sourceId, chunk count, no raw content.

**Files to touch:**
- `src/AiSa.Infrastructure/PgVectorVectorStore.cs` (new)
- `src/AiSa.Infrastructure/PgVectorOptions.cs` (new, if not inlined)
- `src/AiSa.Infrastructure/AiSa.Infrastructure.csproj` (add Npgsql, pgvector packages)

**Minimal test:**
- Unit test with real Postgres+pgvector (e.g. Testcontainers) or in-memory/shim: AddDocumentsAsync stores chunks; SearchAsync returns results ordered by similarity; DeleteBySourceIdAsync removes chunks for source.
- Same ingestion and retrieval contract as AzureSearch (same interface).

**Acceptance:**
- Same ingestion and retrieval works with pgvector.
- 1536-dimensional vectors; cosine or dot-product similarity consistent with existing behavior.

---

### T03.C: .NET Aspire (AppHost) + README
**Scope:**
- In the existing **AiSa.AppHost** project: add a Postgres container using an image that includes the pgvector extension (e.g. `pgvector/pgvector:pg16` via `AddContainer` with image, or use Aspire.Hosting.PostgreSQL with custom image if supported).
- Configure the container (port, database name); use Aspire’s default password parameter or environment so that no secrets are hardcoded (DoD: no secrets in config/source).
- Pass the Postgres connection string to **AiSa.Host** via Aspire reference (e.g. `WithReference` so that the Host receives connection string as config/env when running under AppHost).
- When running with AppHost, set or infer `VectorStore:Provider=PgVector` for the Host so the app uses pgvector in local dev (e.g. via `WithEnvironment` or appsettings in AppHost context).
- README: add section “Running pgvector locally” – run the solution via **AppHost** (e.g. `dotnet run --project src/AiSa.AppHost`), which starts Host + Postgres with pgvector; document that with Provider=PgVector the app uses the Aspire-provisioned Postgres.

**Files to touch:**
- `src/AiSa.AppHost/AppHost.cs` (add Postgres/pgvector container and reference to Host)
- `src/AiSa.AppHost/AiSa.AppHost.csproj` (add package if needed, e.g. Aspire.Hosting.PostgreSQL or use built-in container support)
- `README.md` (add subsection for pgvector local setup with Aspire)

**Minimal test:**
- Run AppHost; Postgres+pgvector container starts; AiSa.Host receives connection string and connects when VectorStore:Provider=PgVector.
- No secrets committed in AppHost or config.

**Acceptance:**
- Local pgvector via .NET Aspire AppHost; README updated; no secrets in config/source.

---

### T03.D: Admin Page – Active Vector Store Provider
**Scope:**
- Add a section to the Admin page (or Admin panel) that displays which vector store provider is currently active (e.g. “AzureSearch” or “PgVector”).
- Data source: config (e.g. inject IOptions<VectorStoreOptions> or read Provider string). No runtime switching – display only.

**Files to touch:**
- `src/AiSa.Host/Components/AdminPanel.razor` (or Admin.razor) – add block showing “Vector store provider: {Provider}”
- Optionally: small API or endpoint that returns current provider for UI (if UI cannot read config directly); or pass from server in existing layout.

**Minimal test:**
- Manual: Set Provider to PgVector, open Admin, see “PgVector”; set to AzureSearch, see “AzureSearch”.

**Acceptance:**
- Admin page section shows which provider is active.

---

### T03.E: ADR (AzureSearch vs pgvector) + Benchmark Script
**Scope:**
- **ADR:** Create new ADR (e.g. `docs/adr/0009-vectorstore-azure-search-vs-pgvector.md`) documenting:
  - Trade-offs: cost (managed vs self-hosted), ops (managed vs Postgres maintenance), features (e.g. semantic ranker, scale-out), portability.
  - Decision: support both via config toggle; when to choose which.
- **Benchmark script:** Minimal script (e.g. Python in `eval/` or script in repo) that:
  - Runs 30 vector queries (e.g. against existing API or directly against store).
  - Outputs average latency (e.g. in ms).
  - Can be run for either provider (config or argument).

**Files to touch:**
- `docs/adr/0009-vectorstore-azure-search-vs-pgvector.md` (new)
- `eval/benchmark_vectorstore.py` or `scripts/benchmark-vectorstore.ps1` (new) – 30 queries, print avg latency

**Minimal test:**
- Run benchmark script with provider=AzureSearch and provider=PgVector (or local pgvector); script completes and prints avg latency.

**Acceptance:**
- ADR added; minimal benchmark script runs 30 queries and outputs avg latency.

---

## Minimal Tests Per Sub-Task

| Sub-Task | Test Type | Test Description |
|----------|-----------|-------------------|
| T03.A | Integration/Startup | App starts with VectorStore:Provider=AzureSearch and PgVector; correct IVectorStore resolved |
| T03.B | Unit/Integration | PgVectorVectorStore AddDocumentsAsync, SearchAsync, DeleteBySourceIdAsync behave per IVectorStore contract |
| T03.C | Manual | Run AppHost; Postgres+pgvector starts; app connects when Provider=PgVector; README instructions work; no secrets in config |
| T03.D | Manual | Admin page shows active provider for AzureSearch and PgVector |
| T03.E | Manual | ADR present; benchmark script runs 30 queries and outputs avg latency |

---

## Risks & Open Questions

### Risks
1. **Connection string secrecy** – Password in connection string must not be in config. Mitigation: use env-only for password (e.g. `PgVector__Password` or `ConnectionString` built from env).
2. **pgvector version/support** – Ensure image and Npgsql driver support same vector dimensions and index type. Mitigation: pin image and package versions; document in README.
3. **Index creation race** – Multiple app instances may try to create table/index. Mitigation: use IF NOT EXISTS and idempotent migrations; optional lock/log.
4. **Resilience parity** – architecture.md mentions retry/timeouts for Azure AI Search. Mitigation: apply similar timeout/retry for pgvector (configurable) in T03.B or follow-up.

### Open Questions
1. **ADR number:** Use next available (0009) for AzureSearch vs pgvector trade-off. Confirm no conflict with existing ADR list.
2. **Benchmark scope:** Script may call `/api/chat` 30 times (includes LLM) or a dedicated “search-only” endpoint. Decision: prefer search-only endpoint or direct store call for latency clarity; if not available, document that benchmark includes LLM latency.
3. **Similarity metric:** pgvector supports cosine, L2. Align with Azure (cosine) for comparable results.

---

## Branch & Commit Strategy

### Branch Name
```
feature/T03-vectorstore-pgvector
```

### Commit Message Pattern
- `feat(T03.A): add VectorStore:Provider config and conditional IVectorStore registration`
- `feat(T03.B): implement PgVectorVectorStore with pgvector`
- `chore(T03.C): add Postgres+pgvector in Aspire AppHost and README section`
- `feat(T03.D): show active vector store provider on Admin page`
- `docs(T03.E): ADR 0009 AzureSearch vs pgvector trade-offs; add benchmark script`

---

## Git Commands (DO NOT EXECUTE)

```bash
# Create branch
git checkout -b feature/T03-vectorstore-pgvector

# First commit (after T03.A)
git add src/AiSa.Host/appsettings.json src/AiSa.Host/Program.cs src/AiSa.Infrastructure/PgVectorOptions.cs
git commit -m "feat(T03.A): add VectorStore:Provider config and conditional IVectorStore registration"

# Subsequent commits (placeholders – run after each sub-task)
# git add <files>
# git commit -m "feat(T03.B): implement PgVectorVectorStore with pgvector"
# git add src/AiSa.AppHost/AppHost.cs src/AiSa.AppHost/AiSa.AppHost.csproj README.md
# git commit -m "chore(T03.C): add Postgres+pgvector in Aspire AppHost and README section"
# git add src/AiSa.Host/Components/AdminPanel.razor
# git commit -m "feat(T03.D): show active vector store provider on Admin page"
# git add docs/adr/0009-vectorstore-azure-search-vs-pgvector.md eval/benchmark_vectorstore.py
# git commit -m "docs(T03.E): ADR 0009 AzureSearch vs pgvector; add benchmark script"
```

---

## Architecture & Doc Compliance

- **ADR-0003:** Dual vector store (Azure AI Search + pgvector) with config toggle – satisfied by T03.A and T03.B.
- **docs/architecture.md:** Vector Store Providers (Azure AI Search, pgvector); toggle via configuration – satisfied.
- **docs/security.md:** No secrets in config files – connection string password from env only.
- **DoD:** Provider toggle tested; README updated for pgvector locally; no secrets in config (Aspire handles credentials).

---

## Demo (Acceptance)

1. Run the app via AppHost (`dotnet run --project src/AiSa.AppHost`): Host + Postgres with pgvector start.
2. With VectorStore:Provider=PgVector and connection from Aspire, app uses pgvector.
3. Upload doc → chat → citations still work.

---

## Next Steps

After this plan is approved:
1. Wait for “Implement T03.A” (or “Implement T03.B”, etc.).
2. Implement only the requested sub-task (~300–400 LOC max).
3. Reply with: modified files, exact verification commands, suggested git commit message.
4. Keep repo buildable; touch only files relevant to that sub-task.
