using System.Diagnostics;
using AiSa.Application;
using AiSa.Application.Models;
using AiSa.Host.Components;
using AiSa.Infrastructure;
using Microsoft.FluentUI.AspNetCore.Components;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Fluent UI Blazor
builder.Services.AddFluentUIComponents();

// Register Application and Infrastructure services
builder.Services.AddScoped<ILLMClient, MockLLMClient>();
builder.Services.AddScoped<IChatService, ChatService>();

// Create ActivitySource for custom spans
var activitySource = new ActivitySource("AiSa.Host");

// Configure OpenTelemetry (ADR-0004: telemetry with no-PII logging)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "AiSa.Host", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource(activitySource.Name) // Register custom ActivitySource
        .AddAspNetCoreInstrumentation(options =>
        {
            // Don't log raw request/response bodies (no-PII policy)
            options.Filter = httpContext =>
            {
                // Only trace /api/* endpoints
                return httpContext.Request.Path.StartsWithSegments("/api");
            };
        })
        .AddHttpClientInstrumentation()
        .AddConsoleExporter());

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// API Endpoints
app.MapPost("/api/chat", async (ChatRequest request, IChatService chatService, CancellationToken cancellationToken) =>
{
    // Create telemetry span named "chat.request" (ADR-0004: no raw prompts in logs, only metadata)
    using var activity = activitySource.StartActivity("chat.request");
    activity?.SetTag("chat.request.message_length", request.Message?.Length ?? 0);
    activity?.SetTag("chat.request.has_correlation_id", request.CorrelationId != null);

    var startTime = DateTime.UtcNow;

    try
    {
        var response = await chatService.ProcessChatAsync(request, cancellationToken);

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        activity?.SetTag("chat.request.duration_ms", duration);
        activity?.SetTag("chat.request.success", true);
        activity?.SetTag("chat.response.correlation_id", response.CorrelationId);
        activity?.SetTag("chat.response.length", response.Response?.Length ?? 0);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        activity?.SetTag("chat.request.duration_ms", duration);
        activity?.SetTag("chat.request.success", false);
        activity?.SetTag("chat.request.error_type", ex.GetType().Name);
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

        // Log only metadata, not raw user prompts (ADR-0004)
        return Results.Problem(
            title: "Chat request failed",
            detail: "An error occurred processing the chat request.",
            statusCode: 500);
    }
})
.WithName("ChatApi");

app.Run();
