using Azure.Core;
using Azure.Identity;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<TokenCredential>(_ => builder.Environment.IsDevelopment()
    ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeManagedIdentityCredential = true })
    : new ManagedIdentityCredential(new ManagedIdentityCredentialOptions()));
builder.Services.AddSingleton(services => CompilationQueueSubmissionService.CreateQueueClient(
    services.GetRequiredService<IConfiguration>(),
    services.GetRequiredService<TokenCredential>()));
builder.Services.AddSingleton<ICompilationQueueSubmission>(services => new CompilationQueueSubmissionService(
    services.GetRequiredService<Azure.Storage.Queues.QueueClient>(),
    services.GetRequiredService<ILogger<CompilationQueueSubmissionService>>(),
    createQueueIfMissing: builder.Environment.IsDevelopment()
        && !string.IsNullOrWhiteSpace(builder.Configuration["AzureWebJobsStorage"])));
builder.Services.AddHttpClient<AzureDevOpsClient>();
builder.Services.AddHttpClient<IExternalConnectionHealthCheck, AzureDevOpsHealthCheck>();
builder.Services.AddSingleton<TableReleaseNoteStore>();
builder.Services.AddSingleton<IReleaseNoteStore>(services => services.GetRequiredService<TableReleaseNoteStore>());
builder.Services.AddSingleton<IExternalConnectionHealthCheck>(services => services.GetRequiredService<TableReleaseNoteStore>());
builder.Services.AddSingleton<ExternalConnectionHealthService>();
builder.Services.AddSingleton<ReleaseNoteCompiler>();

builder.Build().Run();
