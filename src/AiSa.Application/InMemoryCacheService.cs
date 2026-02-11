using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiSa.Application;

/// <summary>
/// In-memory cache service implementation with LRU eviction.
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly CacheOptions _options;
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly object _lock = new();
    private readonly LinkedList<string> _accessOrder = new(); // LRU tracking

    public InMemoryCacheService(
        IOptions<CacheOptions> options,
        ILogger<InMemoryCacheService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Start background cleanup task
        _ = Task.Run(CleanupExpiredEntries);
    }

    public T? Get<T>(string key) where T : class
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var entry))
                return null;

            // Check expiration
            if (entry.ExpiresAt < DateTimeOffset.UtcNow)
            {
                RemoveInternal(key);
                return null;
            }

            // Update access order for LRU
            _accessOrder.Remove(key);
            _accessOrder.AddLast(key);

            return entry.Value as T;
        }
    }

    public void Set<T>(string key, T value, TimeSpan? expiration = null) where T : class
    {
        if (string.IsNullOrWhiteSpace(key) || value == null)
            return;

        var expiresAt = DateTimeOffset.UtcNow.Add(expiration ?? TimeSpan.FromHours(_options.DefaultTtlHours));

        lock (_lock)
        {
            // Evict if cache is full (LRU)
            if (_cache.Count >= _options.MaxEntries && !_cache.ContainsKey(key))
            {
                EvictLru();
            }

            // Update access order
            if (_cache.ContainsKey(key))
            {
                _accessOrder.Remove(key);
            }
            _accessOrder.AddLast(key);

            _cache[key] = new CacheEntry
            {
                Value = value,
                ExpiresAt = expiresAt
            };

            _logger.LogDebug("Cached value for key: {Key}, ExpiresAt: {ExpiresAt}", key, expiresAt);
        }
    }

    public void Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        lock (_lock)
        {
            RemoveInternal(key);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _accessOrder.Clear();
            _logger.LogInformation("Cache cleared. Total entries removed: {Count}", _cache.Count);
        }
    }

    private void RemoveInternal(string key)
    {
        if (_cache.Remove(key))
        {
            _accessOrder.Remove(key);
        }
    }

    private void EvictLru()
    {
        if (_accessOrder.First == null)
            return;

        var lruKey = _accessOrder.First.Value;
        RemoveInternal(lruKey);
        _logger.LogDebug("Evicted LRU entry: {Key}", lruKey);
    }

    private async Task CleanupExpiredEntries()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5)); // Run cleanup every 5 minutes

                lock (_lock)
                {
                    var now = DateTimeOffset.UtcNow;
                    var expiredKeys = _cache
                        .Where(kvp => kvp.Value.ExpiresAt < now)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var key in expiredKeys)
                    {
                        RemoveInternal(key);
                    }

                    if (expiredKeys.Any())
                    {
                        _logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache cleanup");
            }
        }
    }

    private class CacheEntry
    {
        public object Value { get; set; } = null!;
        public DateTimeOffset ExpiresAt { get; set; }
    }
}

/// <summary>
/// Configuration options for cache service.
/// </summary>
public class CacheOptions
{
    /// <summary>
    /// Default TTL in hours. Default: 1 hour.
    /// </summary>
    public int DefaultTtlHours { get; set; } = 1;

    /// <summary>
    /// Maximum number of cache entries. Default: 100.
    /// </summary>
    public int MaxEntries { get; set; } = 100;
}

/// <summary>
/// Configuration options for streaming feature.
/// </summary>
public class StreamingOptions
{
    /// <summary>
    /// Enable streaming responses. Default: false.
    /// When disabled, uses standard non-streaming endpoint with full cache support.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Enable automatic fallback to non-streaming if streaming fails. Default: true.
    /// Ensures graceful degradation if streaming encounters errors.
    /// </summary>
    public bool EnableAutomaticFallback { get; set; } = true;

    /// <summary>
    /// Timeout for streaming connection in seconds. Default: 60.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
