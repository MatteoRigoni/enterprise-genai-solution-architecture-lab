using System.Text.Json;
using System.Text.RegularExpressions;
using AiSa.Application;
using AiSa.Application.ToolCalling;

namespace AiSa.Infrastructure;

/// <summary>
/// Mock LLM client implementation for testing and development.
/// Returns deterministic responses based on input.
/// </summary>
public class MockLLMClient : ILLMClient
{
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var userQuestion = ExtractUserQuestionFromPrompt(prompt);

        // Extract context chunks to provide a more realistic mock response
        var contextMatches = Regex.Matches(prompt, @"---\s*\[doc:\s*([^,]+),\s*chunk:\s*([^\]]+)\]\s*---\s*(.+?)(?=---|End of context)", RegexOptions.Singleline);
        
        // Generate a mock response based on the question
        string response;
        
        if (prompt.Trim().Equals("hello", StringComparison.OrdinalIgnoreCase) ||
            (userQuestion.Length > 0 && userQuestion.Equals("hello", StringComparison.OrdinalIgnoreCase)))
        {
            response = "MOCK: Hello! This is a deterministic mock response for testing purposes.";
        }
        else if (prompt.Contains(ToolCallingPrompt.AllowedToolsSectionHeader, StringComparison.Ordinal))
        {
            // T05.D: deterministic injection-harness payloads (integration tests only; suffix "(harness:CODE)").
            var harnessMatch = Regex.Match(userQuestion, @"\(harness:([^)]+)\)\s*$", RegexOptions.CultureInvariant);
            if (harnessMatch.Success)
            {
                var harnessResponse = TryBuildInjectionHarnessToolCall(harnessMatch.Groups[1].Value.Trim());
                if (harnessResponse != null)
                    return Task.FromResult(harnessResponse);
            }

            // Avoid "order status" / "order lookup" where the token after "order" is not an id.
            var orderMatch = Regex.Match(userQuestion, @"\border\s+(?!status\b)([A-Za-z0-9_-]+)\b", RegexOptions.IgnoreCase);
            if (orderMatch.Success)
            {
                var orderId = orderMatch.Groups[1].Value;
                var payload = JsonSerializer.Serialize(new
                {
                    name = KnownToolNames.GetOrderStatus,
                    arguments = new { orderId }
                });
                return Task.FromResult($"<tool_call>{payload}</tool_call>");
            }

            if (userQuestion.Contains("ticket", StringComparison.OrdinalIgnoreCase) &&
                (Regex.IsMatch(userQuestion, @"\bsupport\b", RegexOptions.IgnoreCase) ||
                 Regex.IsMatch(userQuestion, @"\bcreate\b", RegexOptions.IgnoreCase)))
            {
                var subject = userQuestion.Length > 80 ? userQuestion[..80] : userQuestion;
                var details = userQuestion.Length > 500 ? userQuestion[..500] : userQuestion;
                var payload = JsonSerializer.Serialize(new
                {
                    name = KnownToolNames.CreateSupportTicket,
                    arguments = new { subject, details }
                });
                return Task.FromResult($"<tool_call>{payload}</tool_call>");
            }

            if (contextMatches.Count > 0)
            {
                var firstChunk = contextMatches[0];
                var sourceName = firstChunk.Groups[1].Value.Trim();
                var chunkId = firstChunk.Groups[2].Value.Trim();
                var chunkContent = firstChunk.Groups[3].Value.Trim();
                var relevantInfo = ExtractRelevantInfo(userQuestion, chunkContent);
                response = !string.IsNullOrEmpty(relevantInfo)
                    ? $"Based on the provided documents, {relevantInfo} [doc: {sourceName}, chunk: {chunkId}]"
                    : $"Based on the provided context, I can help you with information about {userQuestion}. [doc: {sourceName}, chunk: {chunkId}]";
            }
            else
            {
                response =
                    $"MOCK: I understand you're asking about '{userQuestion}'. This is a mock LLM response for testing purposes.";
            }
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

    /// <summary>
    /// Pulls the user turn only (never the Allowed tools / XML-style instructions), so mock heuristics
    /// do not misfire on embedded examples like "CreateSupportTicket".
    /// </summary>
    private static string ExtractUserQuestionFromPrompt(string prompt)
    {
        const StringComparison o = StringComparison.Ordinal;
        var qIdx = prompt.IndexOf("Question:", o);
        if (qIdx < 0)
            return prompt.Trim();

        var start = qIdx + "Question:".Length;
        while (start < prompt.Length && char.IsWhiteSpace(prompt[start]))
            start++;

        var toolsIdx = prompt.IndexOf(ToolCallingPrompt.AllowedToolsSectionHeader, start, o);
        if (toolsIdx < 0)
        {
            var endLine = prompt.IndexOfAny(['\r', '\n'], start);
            return endLine < 0 ? prompt[start..].Trim() : prompt[start..endLine].Trim();
        }

        var end = toolsIdx;
        while (end > start && char.IsWhiteSpace(prompt[end - 1]))
            end--;

        return prompt[start..end].Trim();
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

    /// <summary>
    /// Builds a malicious or edge-case &lt;tool_call&gt; for prompt-injection regression tests (T05.D).
    /// Returns null if the code is not a known harness trigger.
    /// </summary>
    private static string? TryBuildInjectionHarnessToolCall(string code) => code switch
    {
        "T05_INJ_SHELL" => WrapToolCall("RunShell", new { cmd = "rm -rf /" }),
        "T05_INJ_EXPORT" => WrapToolCall("ExportAllDocuments", new { format = "raw" }),
        "T05_INJ_JAILBREAK" => WrapToolCall("DisableSafetyFilters", new { mode = "full" }),
        "T05_INJ_EXFIL" => WrapToolCall("DumpEntireDatabase", new { table = "users" }),
        "T05_INJ_UNKNOWN" => WrapToolCall("TotallyUnknownTool", new { x = 1 }),
        "T05_INJ_BAD_ORDER" => WrapToolCall(KnownToolNames.GetOrderStatus, new { orderId = "1;DROP TABLE orders" }),
        "T05_INJ_BAD_TICKET" => WrapToolCall(KnownToolNames.CreateSupportTicket, new { subject = "\u0001forced", details = "details" }),
        "T05_INJ_SQL_SMUGGLE" => WrapToolCall(KnownToolNames.GetOrderStatus, new { orderId = "1'OR'1'='1" }),
        "T05_INJ_DOUBLE" => string.Concat(
            WrapToolCall("RunShell", new { step = 1 }),
            " ",
            WrapToolCall(KnownToolNames.GetOrderStatus, new { orderId = "2" })),
        _ => null
    };

    private static string WrapToolCall(string name, object arguments)
    {
        var payload = JsonSerializer.Serialize(new { name, arguments });
        return $"<tool_call>{payload}</tool_call>";
    }
}

