using Api.Services;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddHttpClient<AzureDevOpsClient>();
builder.Services.AddSingleton<IReleaseNoteStore, CosmosReleaseNoteStore>();
builder.Services.AddSingleton<ReleaseNoteCompiler>();

builder.Build().Run();
