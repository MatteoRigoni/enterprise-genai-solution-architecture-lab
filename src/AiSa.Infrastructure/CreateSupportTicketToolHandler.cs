using System.Text.Json;
using AiSa.Application.ToolCalling;
using Microsoft.Extensions.Logging;

namespace AiSa.Infrastructure;

/// <summary>
/// Mock tool: creates a fake support ticket id (allow-listed in T05).
/// </summary>
public sealed class CreateSupportTicketToolHandler : IToolHandler
{
    private readonly ILogger<CreateSupportTicketToolHandler> _logger;

    public CreateSupportTicketToolHandler(ILogger<CreateSupportTicketToolHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => KnownToolNames.CreateSupportTicket;

    public Task<string> ExecuteAsync(ToolCallProposal proposal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!proposal.Arguments.TryGetValue("subject", out var subEl) ||
            !proposal.Arguments.TryGetValue("details", out var detEl))
        {
            _logger.LogWarning("Tool missing subject/details (metadata only). ToolName: {ToolName}", Name);
            return Task.FromResult("Ticket creation failed: missing subject or details.");
        }

        var subject = subEl.ValueKind == JsonValueKind.String ? subEl.GetString()?.Trim() : null;
        var details = detEl.ValueKind == JsonValueKind.String ? detEl.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(details))
        {
            _logger.LogWarning("Tool invalid subject/details (metadata only). ToolName: {ToolName}", Name);
            return Task.FromResult("Ticket creation failed: invalid subject or details.");
        }

        _logger.LogInformation("Tool executed. ToolName: {ToolName}, CorrelationId: {CorrelationId}", Name,
            System.Diagnostics.Activity.Current?.GetBaggageItem("correlation.id") ?? "n/a");

        return Task.FromResult("Support ticket created: TCK-0001 (mock).");
    }
}
