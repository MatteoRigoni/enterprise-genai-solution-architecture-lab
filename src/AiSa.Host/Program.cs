// Program.cs
using System.Diagnostics;
using AiSa.Application;
using AiSa.Application.Models;
using AiSa.Host;
using AiSa.Host.Components;
using AiSa.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// UI (Blazor Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// App services
builder.Services.AddScoped<ILLMClient, MockLLMClient>();
builder.Services.AddScoped<IChatService, ChatService>();

// ActivitySource for custom spans
builder.Services.AddSingleton(new ActivitySource("AiSa.Host"));

// RFC7807 ProblemDetails
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        var http = ctx.HttpContext;
        var activity = Activity.Current;

        // Correlation ID: populated by CorrelationIdMiddleware for request tracing
        if (http.Items.TryGetValue(Correlation.CorrelationIdItemKey, out var corrObj) &&
            corrObj is string corrId &&
            !string.IsNullOrWhiteSpace(corrId))
        {
            ctx.ProblemDetails.Extensions["correlationId"] = corrId;
        }

        // W3C trace/span identifiers: available when OpenTelemetry Activity exists
        // These enable distributed tracing correlation across services
        if (activity is not null)
        {
            ctx.ProblemDetails.Extensions["traceId"] = activity.TraceId.ToString();
            ctx.ProblemDetails.Extensions["spanId"] = activity.SpanId.ToString();
        }

        // Timestamp: RFC 3339 / ISO 8601 format for when the error occurred
        ctx.ProblemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow.ToString("O");

        // Instance: Identifies the specific request path (RFC 7807 requirement)
        if (string.IsNullOrEmpty(ctx.ProblemDetails.Instance))
        {
            ctx.ProblemDetails.Instance = http.Request.Path;
        }

        if (env.IsDevelopment() && ctx.Exception is not null)
        {
            ctx.ProblemDetails.Extensions["exceptionType"] = ctx.Exception.GetType().FullName;
            ctx.ProblemDetails.Extensions["exceptionMessage"] = ctx.Exception.Message;
        }
    };
});

// Global exception handler for API endpoints
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// OpenTelemetry tracing (avoid capturing request/response bodies)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: "AiSa.Host", serviceVersion: "1.0.0"))
    .WithTracing(t => t
        .AddSource("AiSa.Host")
        .AddAspNetCoreInstrumentation(o =>
        {
            o.Filter = http => http.Request.Path.StartsWithSegments("/api");
        })
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

// API pipeline: ProblemDetails for exceptions and for non-success status codes (e.g., 404)
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    api =>
    {
        // Correlation ID middleware: ensures traceability across logs and distributed systems
        api.UseMiddleware<CorrelationIdMiddleware>();

        // Uses IExceptionHandler (GlobalExceptionHandler) + ProblemDetails
        api.UseExceptionHandler();

        // Produces ProblemDetails for non-success status codes (e.g., 404 on /api/*)
        api.UseStatusCodePages();
    });

// UI pipeline: Blazor-friendly status pages
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    ui =>
    {
        ui.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    });

// Blazor UI
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// API endpoints
var api = app.MapGroup("/api");

api.MapPost("/chat", async (
        ChatRequest request,
        IChatService chatService,
        ActivitySource activitySource,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        // Minimal input validation as a 400 (ProblemDetails is still RFC7807)
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: "The request is invalid. Please check your input and try again.");
        }

        // Child span for application work; the incoming HTTP span already exists
        using var activity = activitySource.StartActivity("chat.handle", ActivityKind.Internal);

        // Low-cardinality metadata only (avoid logging raw prompts)
        activity?.SetTag("chat.message.length", request.Message.Length);

        var response = await chatService.ProcessChatAsync(request, cancellationToken);

        activity?.SetTag("chat.response.length", response.Response?.Length ?? 0);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Results.Ok(response);
    })
    .WithName("ChatApi");

app.Run();

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
