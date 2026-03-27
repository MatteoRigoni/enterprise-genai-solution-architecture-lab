namespace AiSa.Application.ToolCalling;

public interface IToolInputValidator
{
    string ToolName { get; }

    ToolInputValidationResult Validate(ToolCallProposal proposal);
}
