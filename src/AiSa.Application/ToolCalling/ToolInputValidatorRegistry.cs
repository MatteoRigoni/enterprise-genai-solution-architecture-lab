namespace AiSa.Application.ToolCalling;

public sealed class ToolInputValidatorRegistry : IToolInputValidatorRegistry
{
    private readonly Dictionary<string, IToolInputValidator> _validators;

    public ToolInputValidatorRegistry(IEnumerable<IToolInputValidator> validators)
    {
        if (validators == null) throw new ArgumentNullException(nameof(validators));
        _validators = new Dictionary<string, IToolInputValidator>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in validators)
        {
            if (string.IsNullOrWhiteSpace(v.ToolName))
                continue;
            _validators[v.ToolName] = v;
        }
    }

    public bool TryGetValidator(string toolName, out IToolInputValidator? validator)
    {
        validator = null;
        if (string.IsNullOrWhiteSpace(toolName))
            return false;
        return _validators.TryGetValue(toolName, out validator);
    }
}
