var builder = DistributedApplication.CreateBuilder(args);

// Postgres with pgvector extension for local vector store (T03)
// Server name must differ from database name (resource names are unique).
var postgres = builder.AddPostgres("postgres-pgvector")
    .WithImage("pgvector/pgvector", "pg16")
    // Persist Postgres data across Aspire runs using a Docker volume
    // (database files under /var/lib/postgresql/data are kept between restarts)
    .WithDataVolume();
var pgvectorDb = postgres.AddDatabase("pgvector");

var aisaHost = builder.AddProject<Projects.AiSa_Host>("aisa-host")
    .WithEnvironment("ASPIRE_ENABLED", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "AiSa.Host")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    // Use PgVector when running via AppHost (local Postgres). Set to "AzureSearch" or remove to use appsettings.
    .WithEnvironment("VectorStore__Provider", "AzureSearch")
    .WaitFor(pgvectorDb)
    .WithReference(pgvectorDb);

builder.Build().Run();
