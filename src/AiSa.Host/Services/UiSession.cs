namespace AiSa.Host.Services;

/// <summary>
/// Implementation of IUiSession that generates a unique GUID for each Blazor circuit instance.
/// </summary>
public class UiSession : IUiSession
{
    /// <inheritdoc/>
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
}
