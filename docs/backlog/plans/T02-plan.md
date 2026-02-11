# T02 - RAG with Azure AI Search (Citations) + Documents Page - Implementation Plan

## Task Overview
Implement document ingestion and retrieval using Azure AI Search as vector store, integrate RAG into chat flow with citations, and build Documents page in portal.

## High-Level Plan (5 Steps)

1. **Vector Store Abstraction & Azure AI Search Implementation** - Define IVectorStore interface (per ADR-0003), implement AzureSearchVectorStore with index management and vector operations
2. **Document Processing Pipeline** - Implement chunking service, embedding service (Azure OpenAI), and document ingestion use case
3. **Retrieval Service & RAG Integration** - Build retrieval service, update ChatService to use retrieval, add citation formatting
4. **Documents API Endpoints** - Create POST /api/documents (upload) and GET /api/documents (list) with telemetry
5. **Documents Page UI** - Build Documents page with file upload and ingestion status list

## Sub-Task Decomposition

### T02.A: Vector Store Abstraction & Azure AI Search Implementation
**Scope:**
- Define `IVectorStore` interface in `AiSa.Application` (per ADR-0003):
  - `AddDocumentsAsync(IEnumerable<DocumentChunk>)` - Index chunks with vectors and metadata
  - `SearchAsync(string query, int topK, CancellationToken)` - Semantic search returning chunks with scores
  - `DeleteBySourceIdAsync(string sourceId, CancellationToken)` - Delete chunks by source (for future use)
- Implement `AzureSearchVectorStore` in `AiSa.Infrastructure`:
  - Use Azure.Search.Documents NuGet package
  - Create/update search index with vector field (1536 dimensions for text-embedding-ada-002 or text-embedding-3-small)
  - Store metadata: `sourceId`, `sourceName`, `chunkId`, `chunkIndex`, `indexedAt`
  - Implement vector search using Azure AI Search vector query syntax
- Add configuration: `AzureSearch:Endpoint`, `AzureSearch:IndexName`, `AzureSearch:ApiKey` (or use managed identity later)
- Follow ADR-0004: No raw document content in logs (only metadata like sourceId, chunkId hash)

**Files to touch:**
- `src/AiSa.Application/IVectorStore.cs` (new)
- `src/AiSa.Application/Models/DocumentChunk.cs` (new, DTO for chunks with vector + metadata)
- `src/AiSa.Application/Models/SearchResult.cs` (new, DTO for search results with score)
- `src/AiSa.Infrastructure/AzureSearchVectorStore.cs` (new)
- `src/AiSa.Infrastructure/AiSa.Infrastructure.csproj` (add Azure.Search.Documents package)
- `src/AiSa.Host/appsettings.json` (add AzureSearch config section)
- `src/AiSa.Host/Program.cs` (register AzureSearchVectorStore)

**Minimal test:**
- Unit test: `AzureSearchVectorStore.AddDocumentsAsync` stores chunks (mock Azure Search client or integration test with test index)
- Unit test: `AzureSearchVectorStore.SearchAsync` returns results ordered by score

**Acceptance:**
- IVectorStore interface defined following ADR-0003 pattern
- AzureSearchVectorStore implements interface
- Index creation/update logic works
- Vector search returns results with metadata
- No raw document content in logs (only metadata)

---

### T02.B: Document Chunking Service
**Scope:**
- Implement `IDocumentChunker` interface in `AiSa.Application`
- Implement chunking strategy (ADR-0008 to be created):
  - Text-based chunking: split by paragraphs/sentences
  - Chunk size: ~500-1000 tokens (configurable, default 800)
  - Overlap: ~100 tokens between chunks (configurable, default 100)
  - Preserve document structure (mark chunk boundaries)
- **Token counting support:**
  - Basic mode: character-based estimation (~4 chars/token)
  - Advanced mode: SharpToken (tiktoken port) with cl100k_base encoding (compatible with text-embedding-ada-002)
  - Configurable via `Chunking:Mode` setting
- **Improved sentence splitting** (Advanced mode):
  - Abbreviation detection (Dr., Mr., etc.)
  - Better handling of punctuation and context
- **Small chunk merging:**
  - Configurable `MinChunkTokens` to merge chunks below threshold
- Create `DocumentChunk` model with:
  - `Content` (text)
  - `ChunkId` (unique identifier)
  - `ChunkIndex` (position in document)
  - `SourceId` (document identifier)
  - `SourceName` (document filename)
- Follow ADR-0004: Log only chunk metadata (chunkId, chunkIndex, sourceId), never raw content

**Files to touch:**
- `src/AiSa.Application/IDocumentChunker.cs` (new)
- `src/AiSa.Application/DocumentChunker.cs` (new, implementation)
- `src/AiSa.Application/Models/DocumentChunk.cs` (update if needed from T02.A)
- `src/AiSa.Application/AiSa.Application.csproj` (add token counting library if needed, or simple character-based estimation)

**Minimal test:**
- Unit test: `DocumentChunker.ChunkAsync` splits text into chunks of correct size with overlap
- Unit test: Chunking preserves document structure (paragraph boundaries)
- Unit test: Empty or very short documents handled gracefully

**Acceptance:**
- Chunking service produces chunks of configured size with overlap
- Chunk metadata includes sourceId, chunkId, chunkIndex
- Unit tests pass
- No raw content in logs

---

### T02.C: Embedding Service & Document Ingestion Pipeline
**Scope:**
- Define `IEmbeddingService` interface in `AiSa.Application`
- Implement `AzureOpenAIEmbeddingService` in `AiSa.Infrastructure`:
  - Use Azure OpenAI embeddings API (text-embedding-ada-002 or text-embedding-3-small)
  - Generate 1536-dimensional vectors for text chunks
  - Batch embedding requests for efficiency
- Implement `IDocumentIngestionService` in `AiSa.Application`:
  - Orchestrates: file parsing → chunking → embedding → indexing
  - Handles file types: plain text (.txt) first, expandable later
  - Returns ingestion status with document metadata
- Add telemetry span: `documents.ingest` with metadata (sourceId, chunkCount, duration)
- Follow ADR-0004: Log only metadata (sourceId, chunkCount, duration), never raw content

**Files to touch:**
- `src/AiSa.Application/IEmbeddingService.cs` (new)
- `src/AiSa.Application/IDocumentIngestionService.cs` (new)
- `src/AiSa.Application/DocumentIngestionService.cs` (new)
- `src/AiSa.Application/Models/IngestionResult.cs` (new, DTO for ingestion status)
- `src/AiSa.Infrastructure/AzureOpenAIEmbeddingService.cs` (new)
- `src/AiSa.Infrastructure/AiSa.Infrastructure.csproj` (add Azure.AI.OpenAI package)
- `src/AiSa.Host/appsettings.json` (add AzureOpenAI config: Endpoint, DeploymentName, ApiKey)
- `src/AiSa.Host/Program.cs` (register services)

**Minimal test:**
- Unit test: `AzureOpenAIEmbeddingService.GenerateEmbeddingAsync` returns 1536-dimensional vector
- Unit test: `DocumentIngestionService.IngestAsync` processes file → chunks → embeddings → indexes
- Integration test: End-to-end ingestion with mock Azure Search (or test index)

**Acceptance:**
- Embedding service generates vectors for text
- Ingestion pipeline processes files end-to-end
- Telemetry span `documents.ingest` created with metadata
- No raw content in logs

---

### T02.D: Retrieval Service & RAG Integration in Chat
**Scope:**
- Implement `IRetrievalService` in `AiSa.Application`:
  - `RetrieveAsync(string query, int topK, CancellationToken)` - Query vector store, return relevant chunks
  - Uses IVectorStore.SearchAsync
  - Returns chunks with scores and metadata
- Update `ChatService` to use retrieval:
  - Before LLM call: retrieve relevant chunks using user query
  - If retrieval returns empty: return "I don't know based on provided documents."
  - If retrieval returns chunks: build prompt with context and citations
  - Format citations: `[doc: {sourceName}, chunk: {chunkId}]`
- Update `ChatResponse` model to include `Citations` list (optional)
- Add telemetry spans:
  - `retrieval.query` (with metadata: queryLength, topK, resultCount)
  - `llm.generate` (existing, but now with context length metadata)
- Create ADR-0008: Chunking strategy and prompt format with citations
- Follow ADR-0004: No raw chunks in logs, only metadata (chunkId hash, sourceId, score)

**Files to touch:**
- `src/AiSa.Application/IRetrievalService.cs` (new)
- `src/AiSa.Application/RetrievalService.cs` (new)
- `src/AiSa.Application/ChatService.cs` (update to use retrieval)
- `src/AiSa.Application/Models/ChatResponse.cs` (add Citations property)
- `src/AiSa.Application/Models/Citation.cs` (new, DTO for citation)
- `src/AiSa.Host/Program.cs` (register RetrievalService)
- `docs/adr/0008-rag-chunking-prompt.md` (new ADR)

**Minimal test:**
- Unit test: `RetrievalService.RetrieveAsync` returns chunks ordered by score
- Unit test: `ChatService.ProcessChatAsync` with empty retrieval returns "I don't know" message
- Unit test: `ChatService.ProcessChatAsync` with retrieval includes citations in response
- Integration test: Chat with ingested document returns answer with citations

**Acceptance:**
- Retrieval service queries vector store correctly
- Chat service uses retrieval before LLM call
- Citations included in response
- "I don't know" message when retrieval empty
- Telemetry spans created with safe metadata
- ADR-0008 created

---

### T02.E: Documents API Endpoints
**Scope:**
- Create POST `/api/documents` endpoint:
  - Accept multipart/form-data file upload
  - Validate file type (text/plain, .txt extension)
  - Call `IDocumentIngestionService.IngestAsync`
  - Return ingestion status (documentId, status, chunkCount)
  - Add telemetry span: `documents.ingest` (already in ingestion service)
- Create GET `/api/documents` endpoint:
  - Return list of ingested documents with metadata:
    - `DocumentId`, `SourceName`, `ChunkCount`, `IndexedAt`, `Status`
  - Simple in-memory store for document metadata (or query vector store metadata)
- Follow ADR-0004: No raw file content in logs (only filename, size, status)
- Add input validation and error handling (400/500 with ProblemDetails)

**Files to touch:**
- `src/AiSa.Host/Endpoints/DocumentEndpoints.cs` (new)
- `src/AiSa.Application/Models/DocumentMetadata.cs` (new, DTO for document list)
- `src/AiSa.Application/IDocumentMetadataStore.cs` (new, simple interface for storing metadata)
- `src/AiSa.Application/InMemoryDocumentMetadataStore.cs` (new, simple implementation)
- `src/AiSa.Host/Program.cs` (register endpoints and metadata store)

**Minimal test:**
- Integration test: POST `/api/documents` with .txt file returns ingestion status
- Integration test: GET `/api/documents` returns list of ingested documents
- Unit test: Invalid file type returns 400 Bad Request

**Acceptance:**
- POST endpoint accepts file upload and triggers ingestion
- GET endpoint returns document list
- Telemetry spans created
- No raw content in logs
- Error handling with ProblemDetails

---

### T02.F: Documents Page UI
**Scope:**
- Update `Documents.razor` page:
  - File upload component (using Fluent UI FileUpload or HTML input)
  - Upload button and progress indicator
  - Document list table showing:
    - Document name
    - Status (Ingesting, Completed, Failed)
    - Chunk count
    - Indexed at timestamp
  - Refresh button to reload document list
- Style following styleguide (dark premium, elevated surfaces)
- Wire up to `/api/documents` endpoints
- Display error messages for failed uploads
- Show loading state during ingestion

**Files to touch:**
- `src/AiSa.Host/Components/Pages/Documents.razor` (update from placeholder)
- `src/AiSa.Host/Components/Pages/Documents.razor.cs` (code-behind for API calls)
- `src/AiSa.Host/Components/Pages/Documents.razor.css` (component-specific styling)

**Minimal test:**
- Manual: Upload .txt file, see it appear in list with status
- Manual: Refresh list, see updated status
- Manual: Upload invalid file type, see error message

**Acceptance:**
- Documents page displays upload UI and document list
- File upload works and triggers ingestion
- Document list shows metadata correctly
- Styling matches styleguide
- Error handling visible to user

---

## Minimal Tests Per Sub-Task

| Sub-Task | Test Type | Test Description |
|----------|-----------|-------------------|
| T02.A | Unit | AzureSearchVectorStore.AddDocumentsAsync stores chunks |
| T02.A | Unit | AzureSearchVectorStore.SearchAsync returns results ordered by score |
| T02.B | Unit | DocumentChunker.ChunkAsync splits text into correct size chunks with overlap |
| T02.B | Unit | Chunking preserves document structure |
| T02.C | Unit | AzureOpenAIEmbeddingService.GenerateEmbeddingAsync returns 1536-dim vector |
| T02.C | Integration | DocumentIngestionService.IngestAsync processes file end-to-end |
| T02.D | Unit | RetrievalService.RetrieveAsync returns chunks ordered by score |
| T02.D | Unit | ChatService with empty retrieval returns "I don't know" message |
| T02.D | Integration | Chat with ingested document returns answer with citations |
| T02.E | Integration | POST /api/documents with .txt file returns ingestion status |
| T02.E | Integration | GET /api/documents returns list of documents |
| T02.F | Manual | Upload file, see it in list with status |

---

## Risks & Open Questions

### Risks
1. **Azure AI Search index creation complexity** - Index schema, vector field configuration, semantic ranking setup. Mitigation: Start with basic vector search, add semantic ranking later if needed.
2. **Embedding API costs** - Azure OpenAI embeddings have per-token costs. Mitigation: Use efficient batching, consider caching embeddings for unchanged chunks (future optimization).
3. **File parsing limitations** - Starting with .txt only. Mitigation: Explicitly scope to text files, document expansion path for PDF/Word later.
4. **Chunking strategy effectiveness** - Different documents may need different strategies. Mitigation: Start with simple paragraph/sentence-based chunking, document in ADR, make configurable.
5. **Citation format consistency** - Need consistent citation format for UI display. Mitigation: Define citation format in ADR-0008, use structured DTO.
6. **Azure Search index management** - Need to handle index creation/update, handle missing index gracefully. Mitigation: Add index creation logic in AzureSearchVectorStore, handle errors gracefully.
7. **Metadata storage** - Simple in-memory store loses data on restart. Mitigation: Acceptable for T02, document that production would use persistent store (future task).

### Open Questions
1. **Embedding model choice**: text-embedding-ada-002 (1536 dims) vs text-embedding-3-small (1536 dims)? **Decision: Use text-embedding-ada-002** (widely supported, good performance, 1536 dimensions)
2. **Chunk size default**: 500 vs 800 vs 1000 tokens? **Decision: 800 tokens** (balance between context and granularity)
3. **Overlap size**: 50 vs 100 vs 200 tokens? **Decision: 100 tokens** (common practice, prevents context loss at boundaries)
4. **TopK for retrieval**: How many chunks to retrieve? **Decision: 3-5 chunks** (configurable, default 3)
5. **Prompt template**: Where to store prompt template with citations? **Decision: In ChatService or separate PromptComposer service** (start in ChatService, extract later if needed)
6. **Document metadata persistence**: In-memory vs database? **Decision: In-memory for T02** (acceptable for demo, document future enhancement)
7. **Index name strategy**: Fixed name vs configurable? **Decision: Configurable via appsettings** (allows multiple environments)
8. **Error handling for Azure Search**: What if index doesn't exist? **Decision: Auto-create index on first use** (with logging)

---

## Branch & Commit Strategy

### Branch Name
```
feature/T02-rag-azure-ai-search
```

### Commit Message Pattern
- `feat(T02.A): add IVectorStore interface and AzureSearchVectorStore implementation`
- `feat(T02.B): implement document chunking service with configurable strategy`
- `feat(T02.C): add embedding service and document ingestion pipeline`
- `feat(T02.D): integrate retrieval service into chat with citations`
- `feat(T02.E): add documents API endpoints (POST/GET)`
- `feat(T02.F): build Documents page UI with upload and status list`
- `docs(T02.D): add ADR-0008 for chunking strategy and prompt format`

---

## Git Commands (DO NOT EXECUTE)

```bash
# Create branch
git checkout -b feature/T02-rag-azure-ai-search

# First commit (after T02.A)
git add src/AiSa.Application/IVectorStore.cs src/AiSa.Application/Models/DocumentChunk.cs src/AiSa.Application/Models/SearchResult.cs src/AiSa.Infrastructure/AzureSearchVectorStore.cs src/AiSa.Infrastructure/AiSa.Infrastructure.csproj src/AiSa.Host/appsettings.json src/AiSa.Host/Program.cs
git commit -m "feat(T02.A): add IVectorStore interface and AzureSearchVectorStore implementation"

# Second commit (after T02.B)
git add src/AiSa.Application/IDocumentChunker.cs src/AiSa.Application/DocumentChunker.cs
git commit -m "feat(T02.B): implement document chunking service with configurable strategy"
```

---

## Architecture Compliance Check

✅ **ADR-0002**: Provider-agnostic LLM interface - Compliant (using existing ILLMClient)
✅ **ADR-0003**: Dual vector store - Compliant (IVectorStore interface, AzureSearchVectorStore implementation, pgvector to be added in T03)
✅ **ADR-0004**: Telemetry + no-PII logging - Compliant (spans with metadata only, no raw content in logs)
✅ **Architecture**: Document Ingestion Pipeline component - Compliant (matches L3 component definition)
✅ **Security**: No raw doc content in logs - Compliant (only metadata logged)
✅ **Governance**: Basic metadata (sourceId, sourceName) - Compliant (full governance in T12)

---

## Dependencies & Prerequisites

- .NET 10 SDK installed
- Azure AI Search service (or emulator for local dev)
- Azure OpenAI service with embeddings deployment (text-embedding-ada-002)
- Azure.Search.Documents NuGet package
- Azure.AI.OpenAI NuGet package
- Configuration: AzureSearch endpoint, index name, API key (or managed identity)
- Configuration: AzureOpenAI endpoint, deployment name, API key

---

## Verification Commands (Final)

After all sub-tasks complete:

```bash
# Build
dotnet build

# Run
dotnet run --project src/AiSa.Host

# Run tests
dotnet test

# Manual verification
# 1. Open browser to https://localhost:5001 (or configured port)
# 2. Navigate to Documents page
# 3. Upload "faq.txt" file
# 4. Verify document appears in list with status "Completed"
# 5. Navigate to Chat page
# 6. Ask question contained in uploaded document
# 7. Verify answer includes citations (doc name + chunk id)
# 8. Ask question NOT in documents
# 9. Verify "I don't know based on provided documents." response
```

---

## Clean Architecture Compliance

### Layer Responsibilities
- **Application Layer (AiSa.Application)**: 
  - Interfaces: `IVectorStore`, `IDocumentChunker`, `IEmbeddingService`, `IDocumentIngestionService`, `IRetrievalService`
  - Services: `DocumentChunker`, `DocumentIngestionService`, `RetrievalService`
  - Models: `DocumentChunk`, `SearchResult`, `Citation`, `IngestionResult`
- **Infrastructure Layer (AiSa.Infrastructure)**:
  - Implementations: `AzureSearchVectorStore`, `AzureOpenAIEmbeddingService`
  - External dependencies: Azure AI Search, Azure OpenAI
- **Host Layer (AiSa.Host)**:
  - API endpoints: `DocumentEndpoints`
  - UI: `Documents.razor`
  - Configuration: appsettings.json
  - Service registration: Program.cs

### Dependency Flow
- **Host → Application → Infrastructure**: Host registers services, Application defines interfaces, Infrastructure implements
- **No circular dependencies**: Application doesn't depend on Infrastructure
- **Configuration**: All external service configs in appsettings.json (no hardcoded values)

---

## Next Steps

After this plan is approved:
1. Wait for "Implement T02.A" message
2. Implement only T02.A (Vector Store Abstraction)
3. Provide modified files + verification commands + commit message
4. Repeat for each sub-task sequentially (T02.A → T02.B → T02.C → T02.D → T02.E → T02.F)

---

## Notes

- **Minimal scope for T02**: Text files only, simple chunking, basic metadata. Governance features (classification, approval) deferred to T12.
- **Telemetry**: All spans must follow ADR-0004 (metadata only, no raw content).
- **Citations format**: Simple format `[doc: {sourceName}, chunk: {chunkId}]` for T02. Can be enhanced later.
- **Error handling**: Graceful degradation - if retrieval fails, fall back to "I don't know" message.
- **Index management**: Auto-create index on first use. Document manual index creation for production.