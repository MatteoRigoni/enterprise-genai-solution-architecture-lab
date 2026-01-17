# Document Chunking Strategies

## Overview

The document chunking service supports multiple modes and strategies for splitting documents into chunks suitable for embedding and vector search.

## Token Counting Modes

### Basic Mode
- **Method**: Character-based estimation (~4 characters per token)
- **Use case**: Quick development, simple documents
- **Accuracy**: Approximate, may vary for non-English text
- **Performance**: Very fast
- **Configuration**: `Chunking:Mode = "Basic"`

### Advanced Mode
- **Method**: SharpToken (tiktoken port for .NET) with `cl100k_base` encoding
- **Use case**: Production, precise token counting
- **Accuracy**: Exact token count matching text-embedding-ada-002 and GPT-4
- **Performance**: Fast (in-memory tokenizer)
- **Configuration**: `Chunking:Mode = "Advanced"`
- **Tokenizer**: Automatically uses `cl100k_base` encoding (compatible with Azure OpenAI models)

## Chunking Strategy

The service uses a **recursive chunking** approach:

1. **Paragraph-level splitting**: Divides text by double newlines (paragraph breaks)
2. **Sentence-level splitting**: If a paragraph exceeds chunk size, splits by sentences
3. **Overlap handling**: Maintains configurable overlap between chunks to preserve context
4. **Small chunk merging**: Optionally merges chunks below `MinChunkTokens` threshold

### Sentence Splitting

#### Basic Mode
- Simple pattern matching: splits on `. ! ?` followed by whitespace
- Fast but may incorrectly split on abbreviations

#### Advanced Mode (with `ImproveSentenceSplitting: true`)
- Abbreviation detection for common patterns (Dr., Mr., U.S.A., etc.)
- Context-aware splitting (considers capitalization of next word)
- Better handling of punctuation and quotes

## Configuration

```json
{
  "Chunking": {
    "Mode": "Advanced",  // "Basic" | "Advanced"
    "ChunkSizeTokens": 800,
    "OverlapTokens": 100,
    "MinChunkTokens": 50,
    "Advanced": {
      "TokenizerModel": "cl100k_base",
      "ImproveSentenceSplitting": true
    }
  }
}
```

### Configuration Options

- **Mode**: Token counting method (Basic or Advanced)
- **ChunkSizeTokens**: Target chunk size in tokens (default: 800)
- **OverlapTokens**: Overlap size between chunks in tokens (default: 100)
- **MinChunkTokens**: Minimum chunk size; chunks below this may be merged (default: 50)
- **Advanced.TokenizerModel**: Tokenizer encoding (default: "cl100k_base")
- **Advanced.ImproveSentenceSplitting**: Enable improved sentence splitting (default: true)

## Best Practices

1. **For production**: Use `Mode: "Advanced"` for accurate token counting
2. **Chunk size**: 800 tokens is a good balance between context and granularity
3. **Overlap**: 100 tokens prevents context loss at chunk boundaries
4. **MinChunkTokens**: Set to 50-100 to avoid very small chunks that lose context
5. **Tokenizer alignment**: Ensure tokenizer matches your embedding model:
   - `text-embedding-ada-002` → `cl100k_base` ✓
   - `text-embedding-3-small` → `cl100k_base` ✓
   - `text-embedding-3-large` → `cl100k_base` ✓

## Token Counting Libraries

- **SharpToken**: .NET port of tiktoken, supports cl100k_base encoding
- **Basic estimation**: Simple 4 chars/token approximation for development

## References

- [OpenAI Token Counting Guide](https://help.openai.com/en/articles/8984337-how-can-i-tell-how-many-tokens-a-string-will-have-before-i-try-to-embed-it)
- [SharpToken GitHub](https://github.com/dmitry-brazhenko/SharpToken)
- [RAG Chunking Best Practices](https://www.ibm.com/architectures/papers/rag-cookbook/chunking)
