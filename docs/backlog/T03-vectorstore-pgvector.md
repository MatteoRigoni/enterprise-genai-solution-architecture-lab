# T03 - Add pgvector Vector Store (Portable) + Provider Toggle

## Goal
Support a second vector store (pgvector) and allow switching via config; include local dev setup.

## Scope
- Implement PgVectorVectorStore using PostgreSQL + pgvector.
- Add config setting: VectorStore:Provider = "AzureSearch" | "PgVector"
- Provide local Postgres+pgvector via .NET Aspire (existing AppHost).
- Add Admin page section: show which provider is active.

## Acceptance Criteria
- Same ingestion and retrieval works with pgvector.
- Switching provider does not require code changes, only config.
- Add ADR: trade-off AzureSearch vs pgvector (cost, ops, features).
- Minimal benchmark script: run 30 queries and output avg latency.

## Files / Areas
- src/AiSa.Infrastructure: PgVectorVectorStore
- src/AiSa.Host: config + Admin page
- docs/adr: new ADR
- src/AiSa.AppHost: Postgres+pgvector container and connection to Host

## DoD
- Provider toggle tested
- README updated: how to run pgvector locally
- No secrets in config (Aspire handles credentials)

## Demo
1) Run via AppHost (e.g. `dotnet run --project src/AiSa.AppHost`)
2) Set provider=PgVector and use Aspire-provided Postgres connection
3) Upload doc -> chat -> citations still work

## Sources (passive)
- pgvector official documentation
- YouTube: “Vector DB vs Postgres pgvector”
- Blog: “Avoiding vendor lock-in in GenAI”

### Related context
- docs/architecture.md
- docs/cost-model.md
- docs/finops.md
- docs/adr/0001-hosting-model.md
- docs/adr/0003-vectorstore-dual.md
- docs/adr/0007-finops-budgeting.md