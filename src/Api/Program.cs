using Azure.Core;
using Azure.Identity;
using Api;
using Api.Services;
using Api.Authentication;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication(EasyAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, EasyAuthHandler>(EasyAuthHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options => options.AddPolicy("Compile", policy =>
    policy.RequireAssertion(context => EasyAuthHandler.IsLocalDevelopment(builder.Environment, builder.Configuration) ||
        context.User.Identity?.IsAuthenticated == true)));

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services.AddSingleton<TokenCredential>(_ => EasyAuthHandler.IsLocalDevelopment(builder.Environment, builder.Configuration)
    ? new DefaultAzureCredential()
    : new ManagedIdentityCredential());
builder.Services.AddHttpClient<AzureDevOpsClient>();
builder.Services.AddHttpClient<IExternalConnectionHealthCheck, AzureDevOpsHealthCheck>();
builder.Services.AddSingleton<CosmosReleaseNoteStore>();
builder.Services.AddSingleton<IReleaseNoteStore>(services => services.GetRequiredService<CosmosReleaseNoteStore>());
builder.Services.AddSingleton<IExternalConnectionHealthCheck>(services => services.GetRequiredService<CosmosReleaseNoteStore>());
builder.Services.AddSingleton<ExternalConnectionHealthService>();
builder.Services.AddSingleton<ReleaseNoteCompiler>();

var app = builder.Build();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapReleaseEndpoints();
app.MapHealthEndpoints();
app.Map("/api/{**path}", () => TypedResults.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
