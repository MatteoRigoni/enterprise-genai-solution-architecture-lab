var builder = DistributedApplication.CreateBuilder(args);

var aisaHost = builder.AddProject<Projects.AiSa_Host>("aisa-host")
    .WithEnvironment("ASPIRE_ENABLED", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "AiSa.Host")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

builder.Build().Run();
