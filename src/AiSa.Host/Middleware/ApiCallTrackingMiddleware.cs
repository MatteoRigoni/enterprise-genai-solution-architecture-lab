using AiSa.Host.Services;
using System.Diagnostics;

namespace AiSa.Host.Middleware;

/// <summary>
/// Middleware that tracks API calls for UI sessions.
/// Reads x-ui-session-id header and records metadata into the singleton store.
/// </summary>
internal sealed class ApiCallTrackingMiddleware
{
    private const string UiSessionHeaderName = "x-ui-session-id";
    private readonly RequestDelegate _next;

    public ApiCallTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApiCallStore store)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(UiSessionHeaderName, out var sessionIdHeader) ||
            string.IsNullOrWhiteSpace(sessionIdHeader.ToString()))
        {
            await _next(context);
            return;
        }

        var sessionId = sessionIdHeader.ToString();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var durationMs = stopwatch.ElapsedMilliseconds;

            var record = new ApiCallRecord
            {
                Timestamp = DateTimeOffset.UtcNow,
                Method = method,
                Path = path,
                StatusCode = statusCode,
                DurationMs = durationMs
            };

            store.RecordCall(sessionId, record);
        }
        catch (Exception)
        {
            stopwatch.Stop();
            var durationMs = stopwatch.ElapsedMilliseconds;

            var record = new ApiCallRecord
            {
                Timestamp = DateTimeOffset.UtcNow,
                Method = method,
                Path = path,
                StatusCode = 0,
                DurationMs = durationMs
            };

            store.RecordCall(sessionId, record);

            throw;
        }
    }
}
