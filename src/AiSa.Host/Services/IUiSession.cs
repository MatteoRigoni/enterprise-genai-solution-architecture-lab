namespace AiSa.Host.Services;

/// <summary>
/// Service that provides a unique session identifier for each Blazor circuit.
/// The SessionId is generated once per circuit and remains constant for the lifetime of the circuit.
/// </summary>
public interface IUiSession
{
    /// <summary>
    /// Gets the unique session identifier for this Blazor circuit.
    /// </summary>
    string SessionId { get; }
}
