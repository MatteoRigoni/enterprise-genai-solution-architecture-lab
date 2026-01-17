# T02 - RAG with Azure AI Search (Citations) + Documents Page

## Goal
Implement ingestion and retrieval using Azure AI Search as vector store, and show document workflow in portal.

## Scope
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

## Acceptance Criteria
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

## Files / Areas
- src/AiSa.Infrastructure: AzureSearchVectorStore
- src/AiSa.Application: ingestion use case, retrieval service
- src/AiSa.Host: endpoints + portal Documents page
- docs/adr: new ADR(s)

## DoD
- End-to-end ingestion + chat w citations
- Unit test on chunker
- No raw doc content in logs

## Demo
1) Documents -> upload "faq.txt"
2) Chat -> ask question contained in doc
3) Answer includes citations (doc name + chunk id)

## Sources (passive)
- Microsoft Learn: Azure AI Search vector search + semantic ranking
- YouTube: "RAG architecture explained"
- Blog: "Chunking strategies for RAG"

## Related Documentation
- [Chunking Strategies Guide](./chunking-strategies.md) - Detailed documentation on token counting modes and chunking strategies

### Related context
- docs/architecture.md
- docs/governance.md
- docs/quality.md
- docs/security.md
- docs/adr/0002-llm-provider-abstraction.md
- docs/adr/0003-vectorstore-dual.md
- docs/adr/0004-telemetry-policy.md