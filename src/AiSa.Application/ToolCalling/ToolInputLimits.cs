namespace AiSa.Application.ToolCalling;

/// <summary>Length and format bounds for T05.B tool arguments.</summary>
public static class ToolInputLimits
{
    public const int OrderIdMaxLength = 64;
    public const int SubjectMaxLength = 200;
    public const int DetailsMaxLength = 2000;

    /// <summary>Order id: letters, digits, hyphen, underscore only.</summary>
    public const string OrderIdPattern = @"^[A-Za-z0-9_-]+$";
}
