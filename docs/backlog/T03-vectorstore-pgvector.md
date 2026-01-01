# T03 - Add pgvector Vector Store (Portable) + Provider Toggle

## Goal
Support a second vector store (pgvector) and allow switching via config; include local dev setup.

## Scope
- Implement PgVectorVectorStore using PostgreSQL + pgvector.
- Add config setting: VectorStore:Provider = "AzureSearch" | "PgVector"
- Provide local docker compose for Postgres+pgvector.
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
- docker-compose.yml at repo root

## DoD
- Provider toggle tested
- README updated: how to run pgvector locally
- No secrets in compose

## Demo
1) Run docker compose
2) Set provider=PgVector
3) Upload doc -> chat -> citations still work

## Sources (passive)
- pgvector official documentation
- YouTube: “Vector DB vs Postgres pgvector”
- Blog: “Avoiding vendor lock-in in GenAI”
