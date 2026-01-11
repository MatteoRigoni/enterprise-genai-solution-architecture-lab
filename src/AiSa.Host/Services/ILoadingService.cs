namespace AiSa.Host.Services;

/// <summary>
/// Service for centralized loading state management across the application.
/// Supports multiple concurrent loading operations identified by keys.
/// </summary>
public interface ILoadingService
{
    /// <summary>
    /// Event raised when any loading state changes.
    /// </summary>
    event EventHandler<LoadingStateChangedEventArgs>? LoadingStateChanged;

    /// <summary>
    /// Starts a loading operation with the specified key.
    /// </summary>
    /// <param name="key">Unique identifier for the loading operation. If null, uses a default key.</param>
    void StartLoading(string? key = null);

    /// <summary>
    /// Stops a loading operation with the specified key.
    /// </summary>
    /// <param name="key">Unique identifier for the loading operation. If null, uses a default key.</param>
    void StopLoading(string? key = null);

    /// <summary>
    /// Checks if any loading operation is currently active.
    /// </summary>
    /// <returns>True if at least one loading operation is active.</returns>
    bool IsLoading();

    /// <summary>
    /// Checks if a specific loading operation is active.
    /// </summary>
    /// <param name="key">Unique identifier for the loading operation. If null, uses a default key.</param>
    /// <returns>True if the specified loading operation is active.</returns>
    bool IsLoading(string? key);

    /// <summary>
    /// Executes an async operation while managing loading state automatically.
    /// </summary>
    /// <typeparam name="T">Return type of the operation.</typeparam>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="key">Unique identifier for the loading operation. If null, uses a default key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<T> ExecuteWithLoadingAsync<T>(Func<CancellationToken, Task<T>> operation, string? key = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an async operation while managing loading state automatically (void return).
    /// </summary>
    /// <param name="operation">The async operation to execute.</param>
    /// <param name="key">Unique identifier for the loading operation. If null, uses a default key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExecuteWithLoadingAsync(Func<CancellationToken, Task> operation, string? key = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Event arguments for loading state changes.
/// </summary>
public class LoadingStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// The key of the loading operation that changed.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Whether the loading operation is now active.
    /// </summary>
    public bool IsLoading { get; init; }

    /// <summary>
    /// Whether any loading operation is currently active.
    /// </summary>
    public bool AnyLoading { get; init; }
}

