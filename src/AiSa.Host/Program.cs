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
    // Generate or use correlation ID (server-side generation for reliability)
    var correlationId = request.CorrelationId 
        ?? Activity.Current?.Id 
        ?? Guid.NewGuid().ToString();
    
    // Create telemetry span named "chat.request" (ADR-0004: no raw prompts in logs, only metadata)
    using var activity = activitySource.StartActivity("chat.request");
    
    // Set correlation ID in Activity Baggage for automatic propagation to all child spans
    // This ensures correlation ID is available in retrieval.query, llm.generate, etc. without manual passing
    activity?.SetBaggage("correlation.id", correlationId);
    
    // Low-cardinality tags only (ADR-0004 compliant: metadata, not raw content)
    activity?.SetTag("chat.message.length", request.Message?.Length ?? 0);
    activity?.SetTag("chat.request.has_client_correlation_id", request.CorrelationId != null);
    activity?.SetTag("correlation.id", correlationId); // Single tag for filtering/searching (also in Baggage for propagation)

    // Use Stopwatch for high-precision duration measurement (immune to clock adjustments)
    var stopwatch = Stopwatch.StartNew();

    try
    {
        var response = await chatService.ProcessChatAsync(request, cancellationToken);

        stopwatch.Stop();
        activity?.SetTag("chat.duration_ms", stopwatch.ElapsedMilliseconds);
        activity?.SetTag("chat.success", true);
        activity?.SetTag("chat.response.length", response.Response?.Length ?? 0);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        activity?.SetTag("chat.duration_ms", stopwatch.ElapsedMilliseconds);
        activity?.SetTag("chat.success", false);
        activity?.SetTag("chat.error.type", ex.GetType().Name); // Low cardinality (exception type name)
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
