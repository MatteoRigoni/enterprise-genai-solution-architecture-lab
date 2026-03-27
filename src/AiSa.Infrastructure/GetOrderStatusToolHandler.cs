using System.Text.Json;
using AiSa.Application.ToolCalling;
using Microsoft.Extensions.Logging;

namespace AiSa.Infrastructure;

/// <summary>
/// Mock tool: returns a deterministic order status (allow-listed in T05).
/// </summary>
public sealed class GetOrderStatusToolHandler : IToolHandler
{
    public const string ToolName = "GetOrderStatus";

    private readonly ILogger<GetOrderStatusToolHandler> _logger;

    public GetOrderStatusToolHandler(ILogger<GetOrderStatusToolHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string Name => ToolName;

    public Task<string> ExecuteAsync(ToolCallProposal proposal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!proposal.Arguments.TryGetValue("orderId", out var idEl))
        {
            _logger.LogWarning("Tool missing orderId (metadata only). ToolName: {ToolName}", Name);
            return Task.FromResult("Order lookup failed: missing orderId.");
        }

        var orderId = idEl.ValueKind == JsonValueKind.String ? idEl.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(orderId))
        {
            _logger.LogWarning("Tool invalid orderId (metadata only). ToolName: {ToolName}", Name);
            return Task.FromResult("Order lookup failed: invalid orderId.");
        }

        _logger.LogInformation("Tool executed. ToolName: {ToolName}, CorrelationId: {CorrelationId}", Name,
            System.Diagnostics.Activity.Current?.GetBaggageItem("correlation.id") ?? "n/a");

        return Task.FromResult($"Order {orderId} status: Shipped (mock).");
    }
}
