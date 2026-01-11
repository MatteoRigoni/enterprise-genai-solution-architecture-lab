using System.Diagnostics;

namespace AiSa.Host.Middleware;

/// <summary>
/// Correlation ID constants for request tracing.
/// </summary>
internal static class Correlation
{
    public const string HeaderName = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "correlation.id";
}

/// <summary>
/// Ensures every API request has a correlation id available to logs/traces and returned in the response header.
/// </summary>
internal sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId =
            context.Request.Headers.TryGetValue(Correlation.HeaderName, out var headerVal) &&
            !string.IsNullOrWhiteSpace(headerVal.ToString())
                ? headerVal.ToString()
                : Guid.NewGuid().ToString("N");

        context.Items[Correlation.CorrelationIdItemKey] = correlationId;
        context.Response.Headers[Correlation.HeaderName] = correlationId;

        // Propagate correlation ID to OpenTelemetry Activity for distributed tracing
        // Activity.Current may be null if tracing is disabled or not yet initialized
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetBaggage(Correlation.CorrelationIdItemKey, correlationId);
            activity.SetTag(Correlation.CorrelationIdItemKey, correlationId);
        }

        await _next(context);
    }
}


