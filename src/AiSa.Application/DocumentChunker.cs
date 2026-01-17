using AiSa.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace AiSa.Application;

/// <summary>
/// Document chunking service implementation.
/// Splits documents into chunks with configurable size and overlap.
/// </summary>
public class DocumentChunker : IDocumentChunker
{
    private readonly ChunkingOptions _options;
    private readonly ILogger<DocumentChunker> _logger;
    private readonly ITokenCounter _tokenCounter;

    public DocumentChunker(
        IOptions<ChunkingOptions> options,
        ITokenCounter tokenCounter,
        ILogger<DocumentChunker> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<DocumentChunk>> ChunkAsync(
        string content,
        string sourceId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogInformation(
                "Empty content provided for chunking. SourceId: {SourceId}, SourceName: {SourceName}",
                sourceId,
                sourceName);
            return Task.FromResult(Enumerable.Empty<DocumentChunk>());
        }

        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be null or empty", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("SourceName cannot be null or empty", nameof(sourceName));

        // Log metadata only (ADR-0004: no raw content)
        var contentLength = content.Length;
        var totalTokens = _tokenCounter.CountTokens(content);
        _logger.LogInformation(
            "Chunking document. SourceId: {SourceId}, SourceName: {SourceName}, ContentLength: {ContentLength}, TotalTokens: {TotalTokens}, Tokenizer: {Tokenizer}",
            sourceId,
            sourceName,
            contentLength,
            totalTokens,
            _tokenCounter.ModelName);

        var chunks = new List<DocumentChunk>();

        // If content is smaller than chunk size, return single chunk
        if (totalTokens <= _options.ChunkSizeTokens)
        {
            var singleChunk = CreateChunk(content, sourceId, sourceName, 0);
            chunks.Add(singleChunk);
            _logger.LogInformation(
                "Document fits in single chunk. SourceId: {SourceId}, ChunkId: {ChunkId}, Tokens: {Tokens}",
                sourceId,
                singleChunk.ChunkId,
                totalTokens);
            return Task.FromResult<IEnumerable<DocumentChunk>>(chunks);
        }

        // Split by paragraphs first (double newlines)
        var paragraphs = SplitByParagraphs(content);
        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentChunkText = currentChunk.ToString();
            var currentChunkTokens = _tokenCounter.CountTokens(currentChunkText);
            var paragraphTokens = _tokenCounter.CountTokens(paragraph);
            var separatorTokens = currentChunk.Length > 0 ? _tokenCounter.CountTokens("\n\n") : 0;

            // If adding this paragraph would exceed chunk size, finalize current chunk
            if (currentChunk.Length > 0 &&
                (currentChunkTokens + separatorTokens + paragraphTokens) > _options.ChunkSizeTokens)
            {
                var chunkText = currentChunkText.Trim();
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(CreateChunk(chunkText, sourceId, sourceName, chunkIndex++));
                }

                // Start new chunk with overlap from previous chunk
                currentChunk.Clear();
                if (chunks.Count > 0 && _options.OverlapTokens > 0)
                {
                    var previousChunk = chunks[chunks.Count - 1].Content;
                    var overlapText = GetOverlapText(previousChunk, _options.OverlapTokens);
                    if (!string.IsNullOrEmpty(overlapText))
                    {
                        currentChunk.Append(overlapText);
                    }
                }
            }

            // Add paragraph to current chunk
            if (currentChunk.Length > 0)
            {
                currentChunk.Append("\n\n");
            }
            currentChunk.Append(paragraph);

            // If single paragraph exceeds chunk size, split it by sentences
            var currentText = currentChunk.ToString();
            if (_tokenCounter.CountTokens(currentText) > _options.ChunkSizeTokens)
            {
                var oversizedText = currentChunk.ToString();
                currentChunk.Clear();

                // Split oversized paragraph by sentences
                var sentences = SplitBySentences(oversizedText);
                var sentenceChunk = new StringBuilder();

                foreach (var sentence in sentences)
                {
                    var sentenceChunkText = sentenceChunk.ToString();
                    var sentenceChunkTokens = _tokenCounter.CountTokens(sentenceChunkText);
                    var sentenceTokens = _tokenCounter.CountTokens(sentence);
                    var spaceTokens = sentenceChunk.Length > 0 ? _tokenCounter.CountTokens(" ") : 0;

                    if (sentenceChunk.Length > 0 &&
                        (sentenceChunkTokens + spaceTokens + sentenceTokens) > _options.ChunkSizeTokens)
                    {
                        var chunkText = sentenceChunkText.Trim();
                        if (!string.IsNullOrWhiteSpace(chunkText))
                        {
                            chunks.Add(CreateChunk(chunkText, sourceId, sourceName, chunkIndex++));

                            // Add overlap
                            sentenceChunk.Clear();
                            if (_options.OverlapTokens > 0)
                            {
                                var overlapText = GetOverlapText(chunkText, _options.OverlapTokens);
                                if (!string.IsNullOrEmpty(overlapText))
                                {
                                    sentenceChunk.Append(overlapText);
                                }
                            }
                        }
                    }

                    if (sentenceChunk.Length > 0)
                    {
                        sentenceChunk.Append(" ");
                    }
                    sentenceChunk.Append(sentence);
                }

                // Add remaining sentences as final chunk
                if (sentenceChunk.Length > 0)
                {
                    var chunkText = sentenceChunk.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        chunks.Add(CreateChunk(chunkText, sourceId, sourceName, chunkIndex++));
                    }
                }
            }
        }

        // Add final chunk if there's remaining content
        if (currentChunk.Length > 0)
        {
            var chunkText = currentChunk.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                chunks.Add(CreateChunk(chunkText, sourceId, sourceName, chunkIndex));
            }
        }

        // Merge small chunks with next chunk if below minimum
        if (_options.MinChunkTokens > 0)
        {
            chunks = MergeSmallChunks(chunks, sourceId, sourceName).ToList();
        }

        // Log metadata only
        _logger.LogInformation(
            "Document chunked into {ChunkCount} chunks. SourceId: {SourceId}, Tokenizer: {Tokenizer}, ChunkIds: {ChunkIdHashes}",
            chunks.Count,
            sourceId,
            _tokenCounter.ModelName,
            string.Join(", ", chunks.Select(c => c.ChunkId.GetHashCode().ToString("X"))));

        return Task.FromResult<IEnumerable<DocumentChunk>>(chunks);
    }

    private IEnumerable<DocumentChunk> MergeSmallChunks(List<DocumentChunk> chunks, string sourceId, string sourceName)
    {
        if (chunks.Count == 0)
            return chunks;

        var merged = new List<DocumentChunk>();
        var i = 0;

        while (i < chunks.Count)
        {
            var current = chunks[i];
            var currentTokens = _tokenCounter.CountTokens(current.Content);

            // If chunk is too small and not the last one, try to merge with next
            if (currentTokens < _options.MinChunkTokens && i < chunks.Count - 1)
            {
                var next = chunks[i + 1];
                var mergedText = current.Content + "\n\n" + next.Content;
                var mergedTokens = _tokenCounter.CountTokens(mergedText);

                // Only merge if combined size doesn't exceed max chunk size
                if (mergedTokens <= _options.ChunkSizeTokens)
                {
                    // Merge chunks
                    merged.Add(new DocumentChunk
                    {
                        ChunkId = $"{sourceId}-chunk-{merged.Count}",
                        ChunkIndex = merged.Count,
                        Content = mergedText,
                        Vector = Array.Empty<float>(),
                        SourceId = sourceId,
                        SourceName = sourceName,
                        IndexedAt = current.IndexedAt
                    });
                    i += 2; // Skip both chunks
                    continue;
                }
            }

            // Keep chunk as is, but update index
            merged.Add(new DocumentChunk
            {
                ChunkId = $"{sourceId}-chunk-{merged.Count}",
                ChunkIndex = merged.Count,
                Content = current.Content,
                Vector = current.Vector,
                SourceId = current.SourceId,
                SourceName = current.SourceName,
                IndexedAt = current.IndexedAt
            });
            i++;
        }

        return merged;
    }

    private static List<string> SplitByParagraphs(string text)
    {
        // Split by double newlines (paragraph breaks)
        var paragraphs = text.Split(
            new[] { "\n\n", "\r\n\r\n", "\r\r" },
            StringSplitOptions.RemoveEmptyEntries);

        return paragraphs.Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
    }

    private List<string> SplitBySentences(string text)
    {
        if (_options.Mode == ChunkingMode.Advanced && 
            _options.Advanced?.ImproveSentenceSplitting == true)
        {
            return SplitBySentencesAdvanced(text);
        }

        return SplitBySentencesBasic(text);
    }

    private static List<string> SplitBySentencesBasic(string text)
    {
        // Simple sentence splitting by common sentence endings
        var sentences = new List<string>();
        var currentSentence = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            currentSentence.Append(ch);

            // Check for sentence endings: . ! ? followed by space or end of string
            if ((ch == '.' || ch == '!' || ch == '?') &&
                (i == text.Length - 1 || char.IsWhiteSpace(text[i + 1])))
            {
                var sentence = currentSentence.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    sentences.Add(sentence);
                }
                currentSentence.Clear();
            }
        }

        // Add remaining text as final sentence if any
        if (currentSentence.Length > 0)
        {
            var sentence = currentSentence.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    private static List<string> SplitBySentencesAdvanced(string text)
    {
        // Improved sentence splitting with abbreviation detection
        var abbreviations = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Dr", "Mr", "Mrs", "Ms", "Prof", "Sr", "Jr", "Inc", "Ltd", "Co",
            "vs", "etc", "e.g", "i.e", "a.m", "p.m", "U.S.A", "U.K", "U.S",
            "No.", "Vol.", "Fig.", "pp.", "vs.", "approx.", "est.", "Ph.D",
            "M.D", "B.A", "M.A", "B.S", "M.S", "etc."
        };

        var sentences = new List<string>();
        var currentSentence = new StringBuilder();
        var words = new List<string>();

        // Split text into words while preserving punctuation
        var wordPattern = new System.Text.RegularExpressions.Regex(@"\S+");
        var matches = wordPattern.Matches(text);

        for (int i = 0; i < matches.Count; i++)
        {
            var word = matches[i].Value;
            words.Add(word);

            // Check if this word ends a sentence
            var endsWithPunctuation = word.EndsWith('.') || word.EndsWith('!') || word.EndsWith('?');
            
            if (endsWithPunctuation)
            {
                // Check if it's an abbreviation
                var wordWithoutPunctuation = word.TrimEnd('.', '!', '?');
                var isAbbreviation = abbreviations.Contains(wordWithoutPunctuation) ||
                                     (wordWithoutPunctuation.Length <= 3 && char.IsUpper(wordWithoutPunctuation[0]));

                // Check if next word starts with capital (likely new sentence)
                var isNextCapital = i < matches.Count - 1 && 
                                   char.IsUpper(matches[i + 1].Value[0]);

                // If not abbreviation and next word is capital, it's likely end of sentence
                if (!isAbbreviation && isNextCapital)
                {
                    // End of sentence
                    foreach (var w in words)
                    {
                        if (currentSentence.Length > 0)
                            currentSentence.Append(" ");
                        currentSentence.Append(w);
                    }

                    var sentence = currentSentence.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }

                    currentSentence.Clear();
                    words.Clear();
                }
            }
        }

        // Add remaining words as final sentence
        if (words.Count > 0 || currentSentence.Length > 0)
        {
            foreach (var w in words)
            {
                if (currentSentence.Length > 0)
                    currentSentence.Append(" ");
                currentSentence.Append(w);
            }

            var sentence = currentSentence.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                sentences.Add(sentence);
            }
        }

        return sentences;
    }

    private string GetOverlapText(string text, int overlapTokens)
    {
        if (string.IsNullOrEmpty(text) || overlapTokens <= 0)
            return string.Empty;

        // Binary search to find the right position for overlap
        var targetTokens = Math.Min(overlapTokens, _tokenCounter.CountTokens(text));
        
        // Start from end and work backwards
        var minLength = 0;
        var maxLength = text.Length;
        var bestPosition = 0;

        // Binary search for the position that gives us closest to targetTokens
        while (minLength < maxLength)
        {
            var mid = (minLength + maxLength) / 2;
            var candidate = text.Substring(text.Length - mid);
            var tokens = _tokenCounter.CountTokens(candidate);

            if (tokens < targetTokens)
            {
                minLength = mid + 1;
                bestPosition = mid;
            }
            else if (tokens > targetTokens)
            {
                maxLength = mid;
            }
            else
            {
                bestPosition = mid;
                break;
            }
        }

        // Try to find a word boundary near the best position
        var overlapText = text.Substring(text.Length - bestPosition);
        
        // Try to start from a word boundary
        var firstSpace = overlapText.IndexOf(' ');
        if (firstSpace > 0 && firstSpace < overlapText.Length / 2)
        {
            overlapText = overlapText.Substring(firstSpace + 1);
        }

        return overlapText;
    }

    private DocumentChunk CreateChunk(string content, string sourceId, string sourceName, int chunkIndex)
    {
        var chunkId = $"{sourceId}-chunk-{chunkIndex}";

        return new DocumentChunk
        {
            ChunkId = chunkId,
            ChunkIndex = chunkIndex,
            Content = content,
            Vector = Array.Empty<float>(), // Will be populated by embedding service
            SourceId = sourceId,
            SourceName = sourceName,
            IndexedAt = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Configuration options for document chunking.
/// </summary>
public class ChunkingOptions
{
    /// <summary>
    /// Chunking mode: Basic (character estimation) or Advanced (tokenizer-based).
    /// </summary>
    public ChunkingMode Mode { get; set; } = ChunkingMode.Basic;

    /// <summary>
    /// Target chunk size in tokens (default: 800).
    /// </summary>
    public int ChunkSizeTokens { get; set; } = 800;

    /// <summary>
    /// Overlap size in tokens between chunks (default: 100).
    /// </summary>
    public int OverlapTokens { get; set; } = 100;

    /// <summary>
    /// Minimum chunk size in tokens. Chunks smaller than this may be merged with next chunk (default: 50).
    /// </summary>
    public int MinChunkTokens { get; set; } = 50;

    /// <summary>
    /// Advanced chunking options (only used when Mode = Advanced).
    /// </summary>
    public AdvancedChunkingOptions? Advanced { get; set; }
}

/// <summary>
/// Chunking mode enumeration.
/// </summary>
public enum ChunkingMode
{
    /// <summary>
    /// Basic mode: uses character-based token estimation (~4 chars per token).
    /// </summary>
    Basic,

    /// <summary>
    /// Advanced mode: uses actual tokenizer (SharpToken) for precise token counting.
    /// </summary>
    Advanced
}

/// <summary>
/// Advanced chunking configuration options.
/// </summary>
public class AdvancedChunkingOptions
{
    /// <summary>
    /// Tokenizer model/encoding to use (default: cl100k_base, compatible with text-embedding-ada-002).
    /// </summary>
    public string TokenizerModel { get; set; } = "cl100k_base";

    /// <summary>
    /// Whether to use improved sentence splitting with abbreviation detection (default: true).
    /// </summary>
    public bool ImproveSentenceSplitting { get; set; } = true;
}
