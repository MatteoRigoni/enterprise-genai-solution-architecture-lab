# ADR-0008: RAG Chunking Strategy and Prompt Format with Citations

## Status
Accepted

## Context
The system implements Retrieval-Augmented Generation (RAG) where documents are chunked, embedded, indexed, and retrieved to provide context to the LLM. We need to define:
1. Chunking strategy (how to split documents)
2. Prompt format (how to structure context and citations)

## Decision

### Chunking Strategy

**Hierarchical chunking:**
1. **Primary split**: By paragraphs (double newlines `\n\n`)
2. **Secondary split**: By sentences (for oversized paragraphs)
3. **Chunk size**: 800 tokens (default, configurable)
4. **Overlap**: 100 tokens between chunks (default, configurable)
5. **Token estimation**: ~4 characters per token (simple approximation, can be upgraded to proper tokenizer)

**Rationale:**
- Paragraph boundaries preserve semantic coherence
- Sentence splitting handles edge cases (very long paragraphs)
- Overlap prevents context loss at chunk boundaries
- 800 tokens balances context granularity with embedding efficiency

### Prompt Format with Citations

**Structure:**
```
System instruction:
- Assistant role (answers based on provided context)
- Citation format: [doc: {sourceName}, chunk: {chunkId}]

Context sections:
--- [doc: {sourceName}, chunk: {chunkId}] ---
{chunk content}

--- End of context ---

Question: {user query}

Answer based on the provided context...
```

**Citation format:**
- Inline format: `[doc: {sourceName}, chunk: {chunkId}]`
- Example: `[doc: faq.txt, chunk: doc-001-chunk-0]`
- Citations are included in ChatResponse.Citations list

**Rationale:**
- Clear separation of context chunks
- Explicit citation format for traceability
- Structured prompt helps LLM distinguish context from question
- Citations enable users to verify sources

## Alternatives

1. **Fixed-size chunks without overlap**: Rejected - loses context at boundaries
2. **Sentence-only chunking**: Rejected - too granular, loses paragraph-level coherence
3. **Semantic chunking (NLP-based)**: Considered for future - requires additional dependencies, complexity
4. **Citation format variations**:
   - `[{sourceName}#{chunkId}]`: Rejected - less readable
   - `(Source: {sourceName}, Chunk: {chunkId})`: Considered - current format chosen for brevity

## Consequences

### Positive
- Hierarchical chunking preserves document structure
- Overlap prevents context loss
- Clear citation format enables source verification
- Structured prompt improves LLM accuracy

### Negative
- Simple token estimation (4 chars/token) may be inaccurate for non-English text
- Fixed chunk size may not fit all document types optimally
- Prompt formatting adds overhead (acceptable for clarity)

## Implementation Notes

- Chunking configured via `Chunking:ChunkSizeTokens` and `Chunking:OverlapTokens` in appsettings
- Prompt format implemented in `ChatService.BuildPromptWithContext`
- Citations returned in `ChatResponse.Citations`
- Future enhancement: Use proper tokenizer (e.g., tiktoken) for accurate token counting

## References
- T02.B: Document Chunking Service
- T02.D: Retrieval Service & RAG Integration in Chat
