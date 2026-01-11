# T15 - Codebase Documentation via MCP + Global Search Integration

## Goal
Enable search and documentation of the project codebase itself using Model Context Protocol (MCP), integrated with the global search toolbar in the portal.

## Scope
- MCP Server implementation:
  - Scan and index codebase structure (files, classes, methods, ADRs, docs)
  - Generate structured documentation metadata
  - Expose codebase context via MCP protocol
- Global Search integration:
  - Implement search endpoint: GET /api/search?q={query}
  - Search across both: external documents (RAG) + codebase (MCP)
  - Unified results with source type indicators
- TopBar search functionality:
  - Connect search input to /api/search endpoint
  - Display results in dropdown or dedicated search page
  - Show source type (Document, Code, ADR, etc.)
- Codebase indexing:
  - Index key files: source code, ADRs, architecture docs
  - Extract metadata: file paths, class names, function signatures, doc comments
  - Store in searchable format (can reuse vector store or separate index)

## Acceptance Criteria
- Search toolbar returns results from both documents and codebase.
- MCP server exposes codebase structure and can answer questions about project architecture.
- Search results clearly indicate source type (document vs codebase).
- Codebase indexing excludes sensitive files (secrets, keys, etc.).
- Add ADR: MCP integration strategy and codebase indexing approach.
- Telemetry spans: search.query, mcp.codebase.query

## Files / Areas
- src/AiSa.Infrastructure: MCP server implementation
- src/AiSa.Application: search service (unified RAG + MCP)
- src/AiSa.Host: search endpoint + TopBar search integration
- docs/adr: new ADR for MCP integration

## DoD
- End-to-end search works from toolbar
- MCP server can answer questions about codebase structure
- Search results include both document and codebase sources
- No sensitive code indexed

## Demo
1) Home -> Search toolbar: type "How does RAG work?"
2) See results from both uploaded documents AND codebase (e.g., ChatEndpoints.cs, T02 task doc)
3) Click result -> navigate to source or show preview
4) Chat: "What vector stores are supported?" -> can reference both docs and code

## Sources (passive)
- Model Context Protocol (MCP) specification
- GitHub: MCP server examples
- Blog: "Codebase-aware AI assistants with MCP"

### Related context
- docs/architecture.md
- docs/adr/0002-llm-provider-abstraction.md (mentions MCP as future consideration)
- docs/backlog/T02-rag-azure-ai-search.md
- docs/backlog/T05-tool-calling-guardrails.md (mentions MCP)

