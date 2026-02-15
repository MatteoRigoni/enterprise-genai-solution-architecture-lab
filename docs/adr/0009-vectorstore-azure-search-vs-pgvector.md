# ADR-0009: Vector Store Choice – Azure AI Search vs pgvector

## Status
Accepted

## Context
The RAG pipeline requires a vector store for document chunks and similarity search. We need a clear trade-off between a managed Azure service (Azure AI Search) and a portable, self-hosted option (PostgreSQL + pgvector) to support both enterprise Azure-first deployments and local/cost-sensitive scenarios.

## Decision
Support **both** vector stores via a single configuration toggle (`VectorStore:Provider` = `AzureSearch` | `PgVector`). No code changes are required to switch; only configuration. Implementations share the same `IVectorStore` interface (ADR-0003).

### When to choose Azure AI Search
- Production on Azure with managed operations
- Need for built-in semantic ranker or other Azure Search–specific features
- Willingness to pay for a managed search service
- Scale and SLA requirements that justify a dedicated search service

### When to choose pgvector
- Local development or demo (e.g. via .NET Aspire AppHost) without Azure costs
- Existing PostgreSQL footprint and preference to avoid an extra managed service
- Cost sensitivity: no per-query or index tier costs beyond Postgres
- Portability and vendor lock-in concerns; same app can run on-prem or other clouds with Postgres

## Trade-offs

| Aspect | Azure AI Search | pgvector |
|--------|-----------------|----------|
| **Cost** | Per index + query volume; higher for small/medium workloads | Postgres only; no extra search fee |
| **Ops** | Fully managed; no DB maintenance | Self-managed Postgres + extensions |
| **Features** | Semantic ranker, faceting, scale-out as a service | Vector search (cosine/L2); standard SQL |
| **Portability** | Azure-bound | Portable (any Postgres with pgvector) |
| **Local dev** | Requires Azure resources or emulator | Docker/Aspire container; no cloud needed |

## Alternatives
- **Single provider only:** Rejected; would not meet portability and local-dev goals.
- **Abstract search API (e.g. OpenSearch):** Deferred; current dual implementation is sufficient for scope.

## Consequences
- Two code paths to maintain (AzureSearchVectorStore, PgVectorVectorStore); mitigated by shared interface and tests.
- Operational playbooks must cover both (connection strings, health checks).
- Benchmarking and capacity planning should be done per provider (see `eval/benchmark_vectorstore.py`).
