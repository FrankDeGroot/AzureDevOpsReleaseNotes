using System.Net;
using System.Text.Json;
using Shared;

namespace Api.IntegrationTests;

public sealed class CompiledReleaseFixture : IAsyncLifetime
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IntegrationSettings Settings { get; private set; } = null!;

    public HttpClient Client { get; private set; } = null!;

    public HttpStatusCode CompileStatus { get; private set; }

    public string CompileBody { get; private set; } = string.Empty;

    public string ExpectedId => $"{Settings.ProjectId}:{Settings.BuildId}";

    public async Task InitializeAsync()
    {
        Settings = IntegrationSettings.FromEnvironment();
        Client = Settings.CreateClient();
        (CompileStatus, CompileBody) = await CompileAsync();
    }

    public async Task<(HttpStatusCode Status, string Body)> CompileAsync()
    {
        // Buffered content: the Functions host drops chunked request bodies without Content-Length.
        using var content = new StringContent(JsonSerializer.Serialize(new CompileRequest
        {
            Organization = Settings.Organization,
            Project = Settings.Project,
            ProjectId = Settings.ProjectId,
            RepositoryId = Settings.RepositoryId,
            BuildId = Settings.BuildId
        }, JsonOptions), System.Text.Encoding.UTF8, "application/json");
        using var response = await Client.PostAsync("releases/compile", content);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }
}
