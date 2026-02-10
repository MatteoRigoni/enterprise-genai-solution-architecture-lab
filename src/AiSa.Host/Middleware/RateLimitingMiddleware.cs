using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AiSa.Host.Middleware;

/// <summary>
/// Rate limiting middleware for API endpoints.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly Dictionary<string, RateLimiter> _rateLimiters = new();
    private readonly object _lock = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply rate limiting to API endpoints
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Get identifier (user ID if authenticated, otherwise IP address)
        var identifier = GetIdentifier(context);
        var endpoint = context.Request.Path.Value ?? "/api";
        var method = context.Request.Method;

        // Determine rate limit based on endpoint
        var (permitsPerMinute, windowName) = GetRateLimitForEndpoint(endpoint, method);

        if (permitsPerMinute > 0)
        {
            var limiterKey = $"{identifier}:{windowName}";
            var limiter = GetOrCreateLimiter(limiterKey, permitsPerMinute);

            var lease = limiter.AttemptAcquire(permitCount: 1);
            if (!lease.IsAcquired)
            {
                // Rate limit exceeded
                var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue.TotalSeconds
                    : 60;

                _logger.LogWarning(
                    "Rate limit exceeded. Identifier: {Identifier}, Endpoint: {Endpoint}, Limit: {Limit}/min",
                    identifier,
                    endpoint,
                    permitsPerMinute);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter = retryAfter.ToString();
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = $"Rate limit exceeded. Maximum {permitsPerMinute} requests per minute allowed.",
                    retryAfter = retryAfter
                }));

                return;
            }

            // Add rate limit headers
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterMeta))
            {
                context.Response.Headers.RetryAfter = retryAfterMeta.TotalSeconds.ToString();
            }
        }

        await _next(context);
    }

    private string GetIdentifier(HttpContext context)
    {
        // Try to get user ID from claims
        var userId = context.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        // Fallback to IP address
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }

    private (int PermitsPerMinute, string WindowName) GetRateLimitForEndpoint(string endpoint, string method)
    {
        if (endpoint.StartsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
        {
            return (_options.ChatRequestsPerMinute, "chat");
        }

        if (endpoint.StartsWith("/api/documents", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            return (_options.DocumentUploadsPerMinute, "documents");
        }

        // No rate limiting for other endpoints
        return (0, "none");
    }

    private RateLimiter GetOrCreateLimiter(string key, int permitsPerMinute)
    {
        lock (_lock)
        {
            if (!_rateLimiters.TryGetValue(key, out var limiter))
            {
                limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
                {
                    TokenLimit = permitsPerMinute,
                    ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                    TokensPerPeriod = permitsPerMinute,
                    AutoReplenishment = true
                });

                _rateLimiters[key] = limiter;
            }

            return limiter;
        }
    }
}

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Maximum chat requests per minute per user/IP. Default: 10.
    /// </summary>
    public int ChatRequestsPerMinute { get; set; } = 10;

    /// <summary>
    /// Maximum document uploads per minute per user/IP. Default: 5.
    /// </summary>
    public int DocumentUploadsPerMinute { get; set; } = 5;
}
