var builder = DistributedApplication.CreateBuilder(args);

// Postgres with pgvector extension for local vector store (T03)
var pgvector = builder.AddPostgres("pgvector")
    .WithImage("pgvector/pgvector", "pg16");
var pgvectorDb = pgvector.AddDatabase("pgvector");

var aisaHost = builder.AddProject<Projects.AiSa_Host>("aisa-host")
    .WithEnvironment("ASPIRE_ENABLED", "true")
    .WithEnvironment("OTEL_SERVICE_NAME", "AiSa.Host")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("VectorStore__Provider", "PgVector")
    .WaitFor(pgvectorDb)
    .WithReference(pgvectorDb);

builder.Build().Run();
