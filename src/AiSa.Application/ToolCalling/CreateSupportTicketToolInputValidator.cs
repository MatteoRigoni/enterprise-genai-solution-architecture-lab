using System.Text.Json;

namespace AiSa.Application.ToolCalling;

public sealed class CreateSupportTicketToolInputValidator : IToolInputValidator
{
    public string ToolName => KnownToolNames.CreateSupportTicket;

    public ToolInputValidationResult Validate(ToolCallProposal proposal)
    {
        if (!proposal.Arguments.TryGetValue("subject", out var subEl) ||
            !proposal.Arguments.TryGetValue("details", out var detEl))
            return ToolInputValidationResult.Fail("Ticket could not be created: invalid request.");

        if (subEl.ValueKind != JsonValueKind.String || detEl.ValueKind != JsonValueKind.String)
            return ToolInputValidationResult.Fail("Ticket could not be created: invalid request.");

        var subject = subEl.GetString()?.Trim();
        var details = detEl.GetString()?.Trim();

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(details))
            return ToolInputValidationResult.Fail("Ticket could not be created: invalid request.");

        if (subject.Length > ToolInputLimits.SubjectMaxLength || details.Length > ToolInputLimits.DetailsMaxLength)
            return ToolInputValidationResult.Fail("Ticket could not be created: invalid request.");

        if (ContainsDisallowedControlChars(subject, allowNewlines: false) ||
            ContainsDisallowedControlChars(details, allowNewlines: true))
            return ToolInputValidationResult.Fail("Ticket could not be created: invalid request.");

        return ToolInputValidationResult.Ok();
    }

    private static bool ContainsDisallowedControlChars(string s, bool allowNewlines)
    {
        foreach (var c in s)
        {
            if (!char.IsControl(c))
                continue;
            if (allowNewlines && (c == '\r' || c == '\n'))
                continue;
            return true;
        }

        return false;
    }
}
