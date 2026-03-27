namespace AiSa.Application.ToolCalling;

public sealed class ToolInputValidationResult
{
    public static ToolInputValidationResult Ok() => new() { IsValid = true };

    public static ToolInputValidationResult Fail(string userSafeMessage) =>
        new() { IsValid = false, UserSafeMessage = userSafeMessage };

    public bool IsValid { get; private init; }

    /// <summary>Safe short message for the assistant response; never echo raw args.</summary>
    public string? UserSafeMessage { get; private init; }
}
