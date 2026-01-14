// GlobalExceptionHandler.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace AiSa.Host;

/// <summary>
/// Global exception handler for API endpoints that converts unhandled exceptions into RFC 7807-compliant ProblemDetails responses.
/// Only handles /api/* endpoints; UI errors are handled by the Blazor pipeline.
/// Maps exception types to appropriate HTTP status codes (400 for client errors, 500 for server errors).
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetails;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetails)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _problemDetails = problemDetails ?? throw new ArgumentNullException(nameof(problemDetails));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Only handle API endpoints; UI errors are handled by the UI pipeline.
        if (!httpContext.Request.Path.StartsWithSegments("/api"))
            return false;

        var statusCode = DetermineStatusCode(exception);

        // Logging strategy: In production, avoid logging exception.Message to prevent PII leakage
        // (ADR-0004: no-PII logging policy). Log only metadata (path, method, status, type).
        // In development, include full exception details for debugging.
        var isDev = httpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment();

        if (isDev)
        {
            _logger.LogError(
                exception,
                "Unhandled exception on API request. Path: {Path}, Method: {Method}, StatusCode: {StatusCode}, ExceptionType: {ExceptionType}",
                httpContext.Request.Path,
                httpContext.Request.Method,
                statusCode,
                exception.GetType().Name);
        }
        else
        {
            _logger.LogError(
                "Unhandled exception on API request. Path: {Path}, Method: {Method}, StatusCode: {StatusCode}, ExceptionType: {ExceptionType}",
                httpContext.Request.Path,
                httpContext.Request.Method,
                statusCode,
                exception.GetType().Name);
        }

        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("error", true);
            activity.SetTag("error.type", exception.GetType().Name);
            activity.SetTag("http.status_code", statusCode);
            activity.SetStatus(ActivityStatusCode.Error);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = TitleForStatus(statusCode),
            Detail = DetailForStatus(statusCode, isDev, exception),
            Instance = httpContext.Request.Path,
            Type = TypeForStatus(statusCode)
        };

        httpContext.Response.StatusCode = statusCode;

        await _problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });

        return true;
    }

    /// <summary>
    /// Maps exception types to HTTP status codes.
    /// Client errors (4xx): ArgumentException, InvalidOperationException (business logic violations).
    /// Server errors (5xx): All other exceptions.
    /// </summary>
    private static int DetermineStatusCode(Exception exception) =>
        exception switch
        {
            ArgumentException or ArgumentNullException or ArgumentOutOfRangeException
                => StatusCodes.Status400BadRequest,

            InvalidOperationException
                => StatusCodes.Status400BadRequest, // Business logic violations are client errors

            UnauthorizedAccessException
                => StatusCodes.Status403Forbidden,

            KeyNotFoundException or FileNotFoundException
                => StatusCodes.Status404NotFound,

            NotSupportedException
                => StatusCodes.Status501NotImplemented,

            _ => StatusCodes.Status500InternalServerError
        };

    private static string TitleForStatus(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status501NotImplemented => "Not Implemented",
            _ => "Internal Server Error"
        };

    private static string DetailForStatus(int statusCode, bool isDev, Exception ex)
    {
        // Keep details generic to avoid leaking sensitive data.
        // In development, include exception type only (still avoids raw messages/payloads).
        if (isDev)
            return $"Unhandled exception: {ex.GetType().Name}";

        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid. Please check your input and try again.",
            StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status501NotImplemented => "This operation is not supported.",
            _ => "An unexpected error occurred while processing your request."
        };
    }

    private static string TypeForStatus(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            StatusCodes.Status403Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            StatusCodes.Status500InternalServerError => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            StatusCodes.Status501NotImplemented => "https://tools.ietf.org/html/rfc7231#section-6.6.2",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
}
