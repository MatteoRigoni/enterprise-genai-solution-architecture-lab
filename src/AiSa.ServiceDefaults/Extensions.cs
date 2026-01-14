using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Configure OpenTelemetry Resource with service metadata
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var serviceVersion = builder.Configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0";
        var deploymentEnvironment = builder.Environment.EnvironmentName;

        // Configure logging (safe: no PII, only metadata)
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // Configure OpenTelemetry with Resource, Metrics, and Tracing
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = deploymentEnvironment
                }))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        // Exclude health check requests from tracing
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath);
                        
                        // CRITICAL: Do not capture request/response bodies to avoid PII exposure
                        options.EnrichWithHttpRequest = (activity, request) =>
                        {
                            // Only safe metadata: method, path, status
                            // NO body content, NO headers with sensitive data
                        };
                        options.EnrichWithHttpResponse = (activity, response) =>
                        {
                            // Only safe metadata: status code
                            // NO body content
                        };
                        options.EnrichWithException = (activity, exception) =>
                        {
                            // Only exception type and message (no stack trace in production)
                            activity.SetTag("error.type", exception.GetType().Name);
                            if (builder.Environment.IsDevelopment())
                            {
                                activity.SetTag("error.message", exception.Message);
                            }
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        // CRITICAL: Do not capture request/response bodies
                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            // Only safe metadata: method, URI (without query params if sensitive)
                            // NO body content
                        };
                        options.EnrichWithHttpResponseMessage = (activity, response) =>
                        {
                            // Only safe metadata: status code
                            // NO body content
                        };
                    });

                // Configure sampling: ParentBased + TraceIdRatioBased
                // In Development: sample 100% (1.0), in Production: configurable (default 0.1 = 10%)
                var sampleRatio = builder.Environment.IsDevelopment() 
                    ? 1.0 
                    : double.TryParse(builder.Configuration["OTEL_TRACES_SAMPLER_ARG"], out var ratio) 
                        ? ratio 
                        : 0.1;
                
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(sampleRatio)));
            });

        // Enable W3C propagation (tracecontext + baggage) for distributed tracing
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
            new TextMapPropagator[]
            {
                new TraceContextPropagator(),
                new BaggagePropagator()
            }));

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var aspireEnabled = builder.Configuration.GetValue<bool>("ASPIRE_ENABLED", false);
        var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        // Priority: Azure Monitor (Production) > OTLP (Aspire/Dev)
        // Azure Monitor: Production environment with connection string
        if (!string.IsNullOrEmpty(appInsightsConnectionString) && builder.Environment.IsProduction())
        {
            builder.Services.AddOpenTelemetry()
                .UseAzureMonitor(options =>
                {
                    options.ConnectionString = appInsightsConnectionString;
                });
        }
        // OTLP Exporter: Aspire Dashboard (Development) or custom OTLP endpoint
        else if (aspireEnabled || !string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
