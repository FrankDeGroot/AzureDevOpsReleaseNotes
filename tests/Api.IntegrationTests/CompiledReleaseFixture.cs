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

    public string? CompileLocation { get; private set; }

    public CompileReleaseAcknowledgment Acknowledgment { get; private set; } = null!;

    public ReleaseNoteDocument CompiledRelease { get; private set; } = null!;

    public string ExpectedId => $"{Settings.ProjectId}:{Settings.BuildId}";

    public async Task InitializeAsync()
    {
        Settings = IntegrationSettings.FromEnvironment();
        Client = Settings.CreateClient();
        var poller = new ReleasePollingClient(Client);
        var expectedReleaseUrl = GetExpectedReleaseUrl();
        using var preflightTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        await poller.EnsureAbsentAsync(expectedReleaseUrl, ExpectedId, preflightTimeout.Token);

        (CompileStatus, CompileBody, CompileLocation) = await CompileAsync();
        if (CompileStatus != HttpStatusCode.Accepted)
        {
            throw new InvalidOperationException($"Compile returned HTTP {(int)CompileStatus}: {CompileBody}");
        }

        Acknowledgment = JsonSerializer.Deserialize<CompileReleaseAcknowledgment>(CompileBody, JsonOptions)
            ?? throw new InvalidDataException($"Compile returned an empty acknowledgment: {CompileBody}");
        if (Acknowledgment.Id != ExpectedId
            || Acknowledgment.ProjectId != Settings.ProjectId
            || string.IsNullOrWhiteSpace(Acknowledgment.ReleaseUrl)
            || CompileLocation != Acknowledgment.ReleaseUrl)
        {
            throw new InvalidDataException($"Compile returned an invalid acknowledgment or Location header: {CompileBody}");
        }

        var releaseUrl = new Uri(Settings.ApiBaseUrl, Acknowledgment.ReleaseUrl);
        CompiledRelease = await poller.PollAsync(
            releaseUrl,
            ExpectedId,
            TimeSpan.FromSeconds(120),
            TimeSpan.FromSeconds(1));
    }

    public async Task<(HttpStatusCode Status, string Body, string? Location)> CompileAsync()
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
        return (response.StatusCode, await response.Content.ReadAsStringAsync(), response.Headers.Location?.OriginalString);
    }

    private Uri GetExpectedReleaseUrl() => new(
        Settings.ApiBaseUrl,
        $"/api/releases/{Uri.EscapeDataString(ExpectedId)}?projectId={Uri.EscapeDataString(Settings.ProjectId)}");

    public Task DisposeAsync()
    {
        Client?.Dispose();
        return Task.CompletedTask;
    }
}
