IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.PlexVis_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health");

builder.Build().Run();
