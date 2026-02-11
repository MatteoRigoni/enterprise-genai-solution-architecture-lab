# Architecture (C4)

## L1: System Context

**Actors:**
- **User**: End user interacting with chat and agent features
- **Admin**: Administrator managing provider config and health checks

**Systems:**
- **AiSa System**: Main application providing GenAI capabilities
- **LLM Provider**: External LLM service (Azure OpenAI, Mock, others)
- **Vector Store**: External vector database (Azure AI Search, pgvector)
- **Identity**: Authentication and authorization provider
- **Observability Backend**: Telemetry collection and dashboards (OpenTelemetry)

**Relationships:**
- User → AiSa System (uses)
- Admin → AiSa System (manages)
- AiSa System → LLM Provider (calls)
- AiSa System → Vector Store (queries)
- AiSa System → Identity (authenticates)
- AiSa System → Observability Backend (emits telemetry)

## L2: Containers

### AiSa.Host
- **Responsibility**: Web application hosting Blazor UI and minimal APIs
- **Technology**: .NET 10 ASP.NET Core + Blazor
- **Protocols**: HTTP/HTTPS, WebSocket (for Blazor SignalR)

### Vector Store Providers
- **Responsibility**: Semantic search and document retrieval
- **Technology**: Azure AI Search, pgvector (PostgreSQL extension)
- **Protocols**: REST API (Azure AI Search), PostgreSQL protocol (pgvector)
- **Note**: Toggle via configuration

### LLM Providers
- **Responsibility**: Language model inference
- **Technology**: Azure OpenAI, Mock (optional others later)
- **Protocols**: REST API (OpenAI-compatible interface)
- **Note**: Provider-agnostic interface

### EvalRunner
- **Responsibility**: Batch evaluation and regression testing
- **Technology**: Console .NET application or Python script
- **Protocols**: File I/O, HTTP (for API calls)
- **Note**: Runs in CI/CD pipeline

### Observability
- **Responsibility**: Telemetry collection, metrics, and dashboards
- **Technology**: OpenTelemetry exporters + dashboards
- **Protocols**: OTLP, HTTP (for dashboard access)
- **Local Dev**: .NET Aspire Dashboard (via OTLP exporter)
- **Production**: Azure Monitor / Application Insights
- **Note**: See [observability-local.md](./observability-local.md) for local development setup

## L3: Components (AiSa.Host)

### Retriever
- **Responsibility**: Semantic search and document retrieval from vector store
- **Interfaces**: Vector store abstraction

### Prompt Composer
- **Responsibility**: Constructs prompts with context and citations
- **Interfaces**: LLM provider abstraction

### Tool Router
- **Responsibility**: Routes and validates tool/function calls (allow-list enforcement)
- **Interfaces**: Tool registry, validation engine

### Citation Builder
- **Responsibility**: Extracts and formats citations from retrieved documents
- **Interfaces**: Retriever, response formatter

### Document Ingestion Pipeline
- **Responsibility**: Chunks, embeds, and indexes documents
- **Interfaces**: Vector store abstraction, embedding service

### Auth Middleware
- **Responsibility**: Authentication and authorization
- **Interfaces**: Identity provider

## Dynamic/Scenarios

### 1. Chat Flow
User → **AiSa.Host** → Retriever → **Vector Store** → Prompt Composer → **LLM Provider** → Citation Builder → Response w/ citations

### 2. Document Ingestion Flow
Admin → **AiSa.Host** → Document Ingestion Pipeline → **Vector Store**

### 3. Tool Calling Flow
User → **AiSa.Host** → Tool Router (validation) → **LLM Provider** → Tool execution → Response

### 4. Agent Flow
User → **AiSa.Host** → Agent orchestrator → Plan → Step loop → Tool Router → **LLM Provider** → Final answer

### 5. Evaluation Flow
CI/CD → **EvalRunner** → Dataset → **AiSa.Host** API → Metrics → Report → CI gate

## API Reference

**Base URL**: `/api`  
**Swagger UI**: Available at `/swagger` (development mode)

### Chat Endpoints

**POST /api/chat** - Send chat message with RAG
- Request: `{ "message": "query" }`
- Response: `{ "response": "...", "correlationId": "...", "messageId": "...", "citations": [...] }`
- Rate limit: 10 requests/minute

**POST /api/chat/stream** - Stream chat response (SSE)
- Request: `{ "message": "query" }`
- Response: Server-Sent Events stream with chunks
- Format: `data: {"type":"chunk","data":"text"}\n\n`

### Document Endpoints

**POST /api/documents** - Upload and ingest document
- Request: `multipart/form-data` with `file` field (.txt only)
- Response: `{ "documentId": "...", "status": "completed", "chunkCount": N }`
- Rate limit: 5 uploads/minute

**GET /api/documents** - List all ingested documents
- Response: Array of document metadata

**PUT /api/documents/{documentId}** - Update document (creates new version)
- Request: `multipart/form-data` with `file` field
- Response: New version metadata with `previousVersionId`

### Feedback Endpoint

**POST /api/feedback** - Submit feedback for chat response
- Request: `{ "messageId": "...", "rating": "positive|negative", "comment": "..." }`
- Response: `{ "success": true }`
- **Purpose**: Collect user ratings (thumbs up/down) to improve retrieval quality over time
- **Storage**: Currently in-memory; migrate to persistent database for production
- **Privacy**: No PII stored; only MessageId, rating, and optional comment

### Error Handling

All errors follow RFC 7807 ProblemDetails format with `correlationId` and `traceId` for tracing.

## Performance & Resilience

### Caching
- **In-memory LRU cache** for LLM responses (SHA256 key, 1h TTL, 100 max entries)
- **Cache hit rate**: ~20-30% for common queries
- **Cost reduction**: ~20-30% on repeated LLM calls

### Streaming
- **Server-Sent Events (SSE)** for incremental LLM responses
- **TTFB improvement**: 200-500ms vs 2-3s for full response
- **Feature flag**: Configurable via `Performance:Streaming:Enabled`
- **Automatic fallback**: Falls back to non-streaming if streaming fails

### Resilience Patterns

**Retry Policies**:
- Azure OpenAI: 3 attempts, exponential backoff (1s, 2s, 4s)
- Azure AI Search: 2 attempts, exponential backoff (500ms, 1s)

**Circuit Breaker**:
- Opens when 50% failure rate in 30s window (min 2 requests)
- Stays open for 60s, then tests service recovery
- Fallback messages returned when circuit is open

**Timeouts**:
- LLM generation: 60s (configurable)
- Embedding generation: 30s (configurable)
- Vector search: 10s (configurable)
- Index operations: 30s (configurable)

**Graceful Fallbacks**:
- Azure OpenAI down → "Service temporarily unavailable"
- Azure AI Search down → "Document search temporarily unavailable"
- Embedding service down → Empty retrieval results
