using Api.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AzureDevOpsClient>();
builder.Services.AddHttpClient<IExternalConnectionHealthCheck, AzureDevOpsHealthCheck>();
builder.Services.AddSingleton<CosmosReleaseNoteStore>();
builder.Services.AddSingleton<IReleaseNoteStore>(services => services.GetRequiredService<CosmosReleaseNoteStore>());
builder.Services.AddSingleton<IExternalConnectionHealthCheck>(services => services.GetRequiredService<CosmosReleaseNoteStore>());
builder.Services.AddSingleton<ExternalConnectionHealthService>();
builder.Services.AddSingleton<ReleaseNoteCompiler>();

builder.Build().Run();
