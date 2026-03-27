namespace AiSa.Application.ToolCalling;

/// <summary>Structured outcome for metadata-only tool proposal audit (T05.C).</summary>
public enum ToolProposalAuditOutcome
{
    BlockedByConfig,
    BlockedNotAllowlisted,
    BlockedNoValidator,
    ValidationFailed,
    Executed,
    ToolError
}
