using System.Text.Json;

namespace AiSa.Application.ToolCalling;

/// <summary>
/// Parsed tool invocation proposal from LLM output (marker + JSON body).
/// </summary>
public sealed class ToolCallProposal
{
    public ToolCallProposal(string name, IReadOnlyDictionary<string, JsonElement> arguments)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, JsonElement> Arguments { get; }
}
