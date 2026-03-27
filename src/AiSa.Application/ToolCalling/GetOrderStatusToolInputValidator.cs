using System.Text.Json;
using System.Text.RegularExpressions;

namespace AiSa.Application.ToolCalling;

public sealed class GetOrderStatusToolInputValidator : IToolInputValidator
{
    private static readonly Regex OrderIdRegex = new(ToolInputLimits.OrderIdPattern, RegexOptions.Compiled);

    public string ToolName => KnownToolNames.GetOrderStatus;

    public ToolInputValidationResult Validate(ToolCallProposal proposal)
    {
        if (!proposal.Arguments.TryGetValue("orderId", out var idEl))
            return ToolInputValidationResult.Fail("Order lookup could not run: invalid request.");

        if (idEl.ValueKind != JsonValueKind.String)
            return ToolInputValidationResult.Fail("Order lookup could not run: invalid request.");

        var orderId = idEl.GetString()?.Trim();
        if (string.IsNullOrEmpty(orderId))
            return ToolInputValidationResult.Fail("Order lookup could not run: invalid request.");

        if (orderId.Length > ToolInputLimits.OrderIdMaxLength)
            return ToolInputValidationResult.Fail("Order lookup could not run: invalid request.");

        if (!OrderIdRegex.IsMatch(orderId))
            return ToolInputValidationResult.Fail("Order lookup could not run: invalid request.");

        return ToolInputValidationResult.Ok();
    }
}
