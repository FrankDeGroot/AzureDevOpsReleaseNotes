using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shared;

namespace Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ReleaseApiIntegrationTests(CompiledReleaseFixture fixture) : IClassFixture<CompiledReleaseFixture>
{
    [Fact]
    public void Compile_ReturnsReleaseNotesFromAzureDevOps()
    {
        Assert.True(fixture.CompileStatus == HttpStatusCode.OK, $"Compile returned HTTP {(int)fixture.CompileStatus}: {fixture.CompileBody}");
        var document = JsonSerializer.Deserialize<ReleaseNoteDocument>(fixture.CompileBody, CompiledReleaseFixture.JsonOptions)!;

        Assert.Equal(fixture.ExpectedId, document.Id);
        Assert.Equal(fixture.Settings.BuildId, document.BuildId);
        Assert.False(string.IsNullOrWhiteSpace(document.BuildNumber));
        Assert.NotEmpty(document.Commits);
        Assert.NotEmpty(document.WorkItems);
    }

    [Fact]
    public async Task GetById_ReadsStoredReleaseFromCosmosDb()
    {
        Assert.Equal(HttpStatusCode.OK, fixture.CompileStatus);

        var document = await fixture.Client.GetFromJsonAsync<ReleaseNoteDocument>(
            $"releases/{Uri.EscapeDataString(fixture.ExpectedId)}?projectId={Uri.EscapeDataString(fixture.Settings.ProjectId)}",
            CompiledReleaseFixture.JsonOptions);

        Assert.NotNull(document);
        Assert.Equal(fixture.ExpectedId, document.Id);
        Assert.Equal(fixture.Settings.BuildId, document.BuildId);
        Assert.NotEmpty(document.Commits);
        Assert.NotEmpty(document.WorkItems);
    }

    [Fact]
    public async Task Recompile_IsIdempotentInProjectList()
    {
        Assert.Equal(HttpStatusCode.OK, fixture.CompileStatus);
        var (status, body) = await fixture.CompileAsync();
        Assert.True(status == HttpStatusCode.OK, $"Second compile returned HTTP {(int)status}: {body}");

        var documents = await fixture.Client.GetFromJsonAsync<List<ReleaseNoteDocument>>(
            $"releases?project={Uri.EscapeDataString(fixture.Settings.Project)}",
            CompiledReleaseFixture.JsonOptions);

        Assert.NotNull(documents);
        Assert.Single(documents, document => document.Id == fixture.ExpectedId);
    }

    [Fact]
    public async Task Health_ReportsCosmosDbAndAzureDevOpsHealthy()
    {
        Assert.Equal(HttpStatusCode.OK, fixture.CompileStatus);

        using var response = await fixture.Client.GetAsync("health");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Health returned HTTP {(int)response.StatusCode}: {body}");

        var health = JsonSerializer.Deserialize<HealthResult>(body, CompiledReleaseFixture.JsonOptions)!;
        Assert.Equal("healthy", health.Status);
        var connections = health.Connections.ToDictionary(connection => connection.Name, connection => connection.Status);
        Assert.Equal("healthy", connections["cosmosDb"]);
        Assert.Equal("healthy", connections["azureDevOps"]);
    }

    private sealed record HealthResult(string Status, List<ConnectionResult> Connections);

    private sealed record ConnectionResult(string Name, string Status);
}
