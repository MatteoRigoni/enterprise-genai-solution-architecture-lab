using System.Text.RegularExpressions;
using AiSa.Application;

namespace AiSa.Infrastructure;

/// <summary>
/// Mock LLM client implementation for testing and development.
/// Returns deterministic responses based on input.
/// </summary>
public class MockLLMClient : ILLMClient
{
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        // Extract the user question from the RAG prompt
        // The prompt format is: ... "Question: {userQuery}" ...
        var questionMatch = Regex.Match(prompt, @"Question:\s*(.+?)(?:\n|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var userQuestion = questionMatch.Success ? questionMatch.Groups[1].Value.Trim() : prompt.Trim();

        // Extract context chunks to provide a more realistic mock response
        var contextMatches = Regex.Matches(prompt, @"---\s*\[doc:\s*([^,]+),\s*chunk:\s*([^\]]+)\]\s*---\s*(.+?)(?=---|End of context)", RegexOptions.Singleline);
        
        // Generate a mock response based on the question
        string response;
        
        if (prompt.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase) || 
            userQuestion.Equals("hello", StringComparison.OrdinalIgnoreCase))
        {
            response = "MOCK: Hello! This is a deterministic mock response for testing purposes.";
        }
        else if (contextMatches.Count > 0)
        {
            // If context is provided, generate a response that references it
            var firstChunk = contextMatches[0];
            var sourceName = firstChunk.Groups[1].Value.Trim();
            var chunkId = firstChunk.Groups[2].Value.Trim();
            var chunkContent = firstChunk.Groups[3].Value.Trim();
            
            // Try to extract relevant information from the chunk content
            var relevantInfo = ExtractRelevantInfo(userQuestion, chunkContent);
            
            if (!string.IsNullOrEmpty(relevantInfo))
            {
                response = $"Based on the provided documents, {relevantInfo} [doc: {sourceName}, chunk: {chunkId}]";
            }
            else
            {
                response = $"Based on the provided context, I can help you with information about {userQuestion}. [doc: {sourceName}, chunk: {chunkId}]";
            }
        }
        else
        {
            // No context provided, return a generic mock response
            response = $"MOCK: I understand you're asking about '{userQuestion}'. This is a mock LLM response for testing purposes.";
        }

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(string prompt, CancellationToken cancellationToken = default)
    {
        // For mock, simulate streaming by yielding words one at a time
        var response = await GenerateAsync(prompt, cancellationToken);
        var words = response.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(50, cancellationToken); // Simulate network delay
            yield return word + " ";
        }
    }

    private static string ExtractRelevantInfo(string question, string context)
    {
        // Simple keyword matching to extract relevant information
        var lowerQuestion = question.ToLowerInvariant();
        var lowerContext = context.ToLowerInvariant();

        // Try to find sentences that contain keywords from the question
        var questionWords = lowerQuestion.Split(new[] { ' ', '?', '.', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3) // Filter out short words
            .ToArray();

        if (questionWords.Length == 0)
            return string.Empty;

        // Find sentences in context that contain question keywords
        var sentences = context.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var relevantSentences = sentences
            .Where(s => questionWords.Any(w => s.ToLowerInvariant().Contains(w)))
            .Take(2)
            .Select(s => s.Trim())
            .ToArray();

        return relevantSentences.Length > 0 
            ? string.Join(" ", relevantSentences) 
            : string.Empty;
    }
}

