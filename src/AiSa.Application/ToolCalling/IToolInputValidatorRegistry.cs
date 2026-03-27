namespace AiSa.Application.ToolCalling;

public interface IToolInputValidatorRegistry
{
    bool TryGetValidator(string toolName, out IToolInputValidator? validator);
}
