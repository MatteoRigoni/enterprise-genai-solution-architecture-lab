namespace AiSa.Application.ToolCalling;

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IToolHandler> _handlers;

    public ToolRegistry(IEnumerable<IToolHandler> handlers)
    {
        if (handlers == null) throw new ArgumentNullException(nameof(handlers));
        _handlers = new Dictionary<string, IToolHandler>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in handlers)
        {
            if (string.IsNullOrWhiteSpace(h.Name))
                continue;
            _handlers[h.Name] = h;
        }
    }

    public bool TryGetHandler(string toolName, out IToolHandler? handler)
    {
        handler = null;
        if (string.IsNullOrWhiteSpace(toolName))
            return false;
        return _handlers.TryGetValue(toolName, out handler);
    }
}
