using System.Diagnostics.CodeAnalysis;

namespace AiSa.Application.ToolCalling;

public interface IToolCallParser
{
    /// <summary>
    /// Parses the first &lt;tool_call&gt;...&lt;/tool_call&gt; block from LLM text, if any.
    /// </summary>
    bool TryParse(string llmResponse, [NotNullWhen(true)] out ToolCallProposal? proposal);
}
