namespace AiSa.Application.ToolCalling;

public interface IToolRegistry
{
    bool TryGetHandler(string toolName, out IToolHandler? handler);
}
