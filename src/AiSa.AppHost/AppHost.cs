var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.AiSa_Host>("aisa-host");

builder.Build().Run();
