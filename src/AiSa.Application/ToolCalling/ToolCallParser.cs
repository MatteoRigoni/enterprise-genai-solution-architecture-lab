using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace AiSa.Application.ToolCalling;

public sealed class ToolCallParser : IToolCallParser
{
    private const string OpenTag = "<tool_call>";
    private const string CloseTag = "</tool_call>";

    public bool TryParse(string llmResponse, [NotNullWhen(true)] out ToolCallProposal? proposal)
    {
        proposal = null;
        if (string.IsNullOrWhiteSpace(llmResponse))
            return false;

        var start = llmResponse.IndexOf(OpenTag, StringComparison.OrdinalIgnoreCase);
        var end = llmResponse.IndexOf(CloseTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end < 0 || end <= start)
            return false;

        var jsonSlice = llmResponse.AsSpan(start + OpenTag.Length, end - start - OpenTag.Length).Trim();
        if (jsonSlice.IsEmpty)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(jsonSlice.ToString());
            var root = doc.RootElement;
            if (!root.TryGetProperty("name", out var nameEl))
                return false;
            var name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var args = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            if (root.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in argsEl.EnumerateObject())
                    args[prop.Name] = prop.Value.Clone();
            }

            proposal = new ToolCallProposal(name.Trim(), args);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
