namespace AiSa.Application.ToolCalling;

public interface IToolHandler
{
    string Name { get; }

    Task<string> ExecuteAsync(ToolCallProposal proposal, CancellationToken cancellationToken = default);
}
