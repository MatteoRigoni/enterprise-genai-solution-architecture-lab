namespace AiSa.Application;

/// <summary>
/// Cache service interface for caching LLM responses and other data.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Gets a value from cache by key.
    /// </summary>
    /// <typeparam name="T">Type of cached value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <returns>Cached value if found, null otherwise.</returns>
    T? Get<T>(string key) where T : class;

    /// <summary>
    /// Sets a value in cache with optional expiration.
    /// </summary>
    /// <typeparam name="T">Type of value to cache.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">Optional expiration time. If null, uses default TTL.</param>
    void Set<T>(string key, T value, TimeSpan? expiration = null) where T : class;

    /// <summary>
    /// Removes a value from cache by key.
    /// </summary>
    /// <param name="key">Cache key.</param>
    void Remove(string key);

    /// <summary>
    /// Clears all cached values.
    /// </summary>
    void Clear();
}
