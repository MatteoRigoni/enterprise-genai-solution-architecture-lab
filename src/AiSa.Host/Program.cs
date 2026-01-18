using AiSa.Application;
using AiSa.Host;
using AiSa.Host.Components;
using AiSa.Host.Endpoints;
using AiSa.Host.Handlers;
using AiSa.Host.Middleware;
using AiSa.Host.Services;
using AiSa.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.FluentUI.AspNetCore.Components;
using System.Diagnostics;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// UI (Blazor Server)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

// Fake Authentication (Cookie-based, for demo purposes)
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
    });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, 
    Microsoft.AspNetCore.Components.Server.ServerAuthenticationStateProvider>();

// UI services
builder.Services.AddScoped<ILoadingService, LoadingService>();
builder.Services.AddScoped<IToastNotificationService, ToastNotificationService>();
builder.Services.AddScoped<IUiSession, UiSession>();

// API call tracking store (singleton)
builder.Services.AddSingleton<IApiCallStore, ApiCallStore>();
builder.Services.AddHostedService<ApiCallStoreCleanupService>();

// Register HttpClient with UI session header handler for Blazor components
builder.Services.AddScoped<HttpClient>(sp =>
{
    var uiSession = sp.GetRequiredService<IUiSession>();
    var handler = new UiSessionHeaderHandler(uiSession)
    {
        InnerHandler = new HttpClientHandler()
    };
    var httpClient = new HttpClient(handler);
    
    // Set base address for relative URLs in Blazor Server
    // Try NavigationManager first (most reliable in Blazor Server), fallback to HttpContext
    var navigationManager = sp.GetService<Microsoft.AspNetCore.Components.NavigationManager>();
    if (navigationManager != null)
    {
        httpClient.BaseAddress = new Uri(navigationManager.BaseUri);
    }
    else
    {
        // Fallback to HttpContext if NavigationManager is not available
        var httpContextAccessor = sp.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        if (httpContextAccessor?.HttpContext != null)
        {
            var request = httpContextAccessor.HttpContext.Request;
            httpClient.BaseAddress = new Uri($"{request.Scheme}://{request.Host}");
        }
    }
    
    return httpClient;
});

// App services
builder.Services.AddScoped<ILLMClient, MockLLMClient>();
builder.Services.AddScoped<IChatService, ChatService>();

// Document chunking - register token counter based on mode
var chunkingOptions = builder.Configuration.GetSection("Chunking").Get<ChunkingOptions>();
if (chunkingOptions?.Mode == ChunkingMode.Advanced)
{
    builder.Services.AddSingleton<ITokenCounter, SharpTokenCounter>();
}
else
{
    builder.Services.AddSingleton<ITokenCounter, BasicTokenCounter>();
}

builder.Services.Configure<ChunkingOptions>(
    builder.Configuration.GetSection("Chunking"));
builder.Services.AddScoped<IDocumentChunker, DocumentChunker>();

// Embedding service (Azure OpenAI)
builder.Services.AddHttpClient(); // For AzureOpenAIEmbeddingService
builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddSingleton<IEmbeddingService, AzureOpenAIEmbeddingService>();

// Document ingestion
builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

// Vector store (Azure AI Search)
builder.Services.Configure<AzureSearchOptions>(
    builder.Configuration.GetSection("AzureSearch"));
builder.Services.AddSingleton<IVectorStore, AzureSearchVectorStore>();

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

        // Development mode: add exception details to ProblemDetails
        if (http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment() && ctx.Exception is not null)
        {
            ctx.ProblemDetails.Extensions["exceptionType"] = ctx.Exception.GetType().FullName;
            ctx.ProblemDetails.Extensions["exceptionMessage"] = ctx.Exception.Message;
        }
    };
});

// Global exception handler for API endpoints
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidation();

// OpenAPI/Swagger documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AiSa API",
        Version = "v1",
        Description = "Enterprise GenAI Solution Architecture Lab - API Documentation"
    });
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// API pipeline: ProblemDetails for exceptions and for non-success status codes (e.g., 404)
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    api =>
    {
        // Correlation ID middleware: ensures traceability across logs and distributed systems
        api.UseMiddleware<CorrelationIdMiddleware>();

        // API call tracking middleware: records metadata for UI sessions
        api.UseMiddleware<ApiCallTrackingMiddleware>();

        // Uses IExceptionHandler (GlobalExceptionHandler) + ProblemDetails
        api.UseExceptionHandler();

        // Produces ProblemDetails for non-success status codes (e.g., 404 on /api/*)
        api.UseStatusCodePages();
    });

// Swagger UI - Only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AiSa API v1");
        options.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

app.UseAntiforgery();
app.MapStaticAssets();

// Blazor UI
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// API endpoints
app.MapChatEndpoints();

// Fake Login/Logout endpoints (for demo purposes)
app.MapGet("/Account/Login", async (HttpContext context) =>
{
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, "Utente Demo"),
        new(ClaimTypes.Email, "utente.demo@aisa.com"),
        new("name", "Utente Demo")
    };
    
    var identity = new ClaimsIdentity(claims, "Cookies");
    var principal = new ClaimsPrincipal(identity);
    
    await context.SignInAsync("Cookies", principal);
    
    return Results.Redirect("/");
});

app.MapGet("/Account/Logout", async (HttpContext context) =>
{
    await context.SignOutAsync("Cookies");
    return Results.Redirect("/");
});

// Fallbacks
app.MapFallback("/api/{*path}", (HttpContext ctx) =>
    Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Not Found",
        instance: ctx.Request.Path));

app.MapFallback("{*path:nonfile}", () =>
    new RazorComponentResult<App>
    {
        StatusCode = StatusCodes.Status404NotFound
    });

app.Run();

// Make Program class accessible for WebApplicationFactory in integration tests
namespace AiSa.Host
{
    public partial class Program { }
}
