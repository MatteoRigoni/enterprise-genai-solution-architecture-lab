namespace AiSa.Host.Services;

/// <summary>
/// Implementation of ILoadingService for centralized loading state management.
/// Uses a dictionary to track multiple concurrent loading operations.
/// </summary>
public class LoadingService : ILoadingService
{
    private const string DefaultKey = "__default__";
    private readonly Dictionary<string, int> _loadingOperations = new();
    private readonly object _lock = new();

    /// <inheritdoc/>
    public event EventHandler<LoadingStateChangedEventArgs>? LoadingStateChanged;

    /// <inheritdoc/>
    public void StartLoading(string? key = null)
    {
        var actualKey = key ?? DefaultKey;
        bool wasLoading;
        bool isNowLoading;

        lock (_lock)
        {
            wasLoading = _loadingOperations.Values.Any(count => count > 0);
            
            if (!_loadingOperations.TryGetValue(actualKey, out var count))
            {
                _loadingOperations[actualKey] = 1;
            }
            else
            {
                _loadingOperations[actualKey] = count + 1;
            }

            isNowLoading = _loadingOperations.Values.Any(c => c > 0);
        }

        // Only raise event if the overall loading state changed
        if (wasLoading != isNowLoading)
        {
            OnLoadingStateChanged(actualKey, true, isNowLoading);
        }
    }

    /// <inheritdoc/>
    public void StopLoading(string? key = null)
    {
        var actualKey = key ?? DefaultKey;
        bool wasLoading;
        bool isNowLoading;

        lock (_lock)
        {
            wasLoading = _loadingOperations.Values.Any(count => count > 0);

            if (_loadingOperations.TryGetValue(actualKey, out var count))
            {
                if (count > 1)
                {
                    _loadingOperations[actualKey] = count - 1;
                }
                else
                {
                    _loadingOperations.Remove(actualKey);
                }
            }

            isNowLoading = _loadingOperations.Values.Any(c => c > 0);
        }

        // Only raise event if the overall loading state changed
        if (wasLoading != isNowLoading)
        {
            OnLoadingStateChanged(actualKey, false, isNowLoading);
        }
    }

    /// <inheritdoc/>
    public bool IsLoading()
    {
        lock (_lock)
        {
            return _loadingOperations.Values.Any(count => count > 0);
        }
    }

    /// <inheritdoc/>
    public bool IsLoading(string? key)
    {
        var actualKey = key ?? DefaultKey;
        lock (_lock)
        {
            return _loadingOperations.TryGetValue(actualKey, out var count) && count > 0;
        }
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteWithLoadingAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        string? key = null,
        CancellationToken cancellationToken = default)
    {
        StartLoading(key);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            StopLoading(key);
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteWithLoadingAsync(
        Func<CancellationToken, Task> operation,
        string? key = null,
        CancellationToken cancellationToken = default)
    {
        StartLoading(key);
        try
        {
            await operation(cancellationToken);
        }
        finally
        {
            StopLoading(key);
        }
    }

    private void OnLoadingStateChanged(string key, bool isKeyLoading, bool anyLoading)
    {
        LoadingStateChanged?.Invoke(this, new LoadingStateChangedEventArgs
        {
            Key = key,
            IsLoading = isKeyLoading,
            AnyLoading = anyLoading
        });
    }
}

