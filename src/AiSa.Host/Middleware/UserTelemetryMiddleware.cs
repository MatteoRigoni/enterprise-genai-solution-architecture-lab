using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AiSa.Host.Middleware;

/// <summary>
/// Enriches OpenTelemetry spans and logs with authenticated user context, without emitting PII.
/// </summary>
internal sealed class UserTelemetryMiddleware
{
    private readonly RequestDelegate _next;

    // Ephemeral per-process fallback key to avoid requiring secrets in dev/demo.
    // In production, prefer configuring Telemetry:User:HmacKey via secret store.
    private static readonly byte[] FallbackHmacKey = RandomNumberGenerator.GetBytes(32);

    public UserTelemetryMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration, ILogger<UserTelemetryMiddleware> logger)
    {
        var activity = Activity.Current;

        var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;
        var authenticationType = context.User?.Identity?.AuthenticationType;

        string? endUserId = null;
        string? endUserIdSource = null;

        if (isAuthenticated)
        {
            // Prefer stable non-PII identifiers (avoid email/name).
            var principal = context.User;
            var (rawId, source) = FindStableUserIdentifier(principal);

            if (!string.IsNullOrWhiteSpace(rawId))
            {
                var key = configuration["Telemetry:User:HmacKey"];
                endUserId = ComputeHmacTruncatedHex(key, rawId);
                endUserIdSource = source;
            }
        }

        // Enrich traces (safe, no PII)
        if (activity is not null)
        {
            activity.SetTag("enduser.authenticated", isAuthenticated);
            if (!string.IsNullOrWhiteSpace(authenticationType))
            {
                activity.SetTag("auth.scheme", authenticationType);
            }

            if (!string.IsNullOrWhiteSpace(endUserId))
            {
                activity.SetTag("enduser.id", endUserId);
                if (!string.IsNullOrWhiteSpace(endUserIdSource))
                {
                    activity.SetTag("enduser.id_source", endUserIdSource);
                }
            }
        }

        // Enrich logs via scope (OTel logging has IncludeScopes enabled in ServiceDefaults)
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["enduser.authenticated"] = isAuthenticated,
            ["enduser.id"] = endUserId,
            ["auth.scheme"] = authenticationType
        }))
        {
            await _next(context);
        }
    }

    private static (string? RawId, string? Source) FindStableUserIdentifier(ClaimsPrincipal principal)
    {
        // Order matters: try common stable IDs first.
        var claim =
            principal.FindFirst("sub")
            ?? principal.FindFirst("oid")
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier");

        return claim is null ? (null, null) : (claim.Value, claim.Type);
    }

    private static string ComputeHmacTruncatedHex(string? configuredKey, string value)
    {
        // If a key is configured, use it; otherwise use ephemeral fallback.
        var keyBytes = string.IsNullOrWhiteSpace(configuredKey)
            ? FallbackHmacKey
            : Encoding.UTF8.GetBytes(configuredKey);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));

        // Keep it short to reduce payload while remaining collision-resistant enough for correlation.
        // 16 bytes => 32 hex chars.
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }
}

