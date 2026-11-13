using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shared;

namespace Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class ReleaseApiIntegrationTests(CompiledReleaseFixture fixture) : IClassFixture<CompiledReleaseFixture>
{
    [Fact]
    public void Compile_ReturnsAcceptanceThenReleaseFromAzureDevOps()
    {
        Assert.Equal(HttpStatusCode.Accepted, fixture.CompileStatus);
        Assert.Equal(fixture.ExpectedId, fixture.Acknowledgment.Id);
        Assert.Equal(fixture.Settings.ProjectId, fixture.Acknowledgment.ProjectId);
        Assert.Equal(fixture.Acknowledgment.ReleaseUrl, fixture.CompileLocation);

        Assert.Equal(fixture.ExpectedId, fixture.CompiledRelease.Id);
        Assert.Equal(fixture.Settings.BuildId, fixture.CompiledRelease.BuildId);
        Assert.False(string.IsNullOrWhiteSpace(fixture.CompiledRelease.BuildNumber));
        Assert.NotEmpty(fixture.CompiledRelease.Commits);
        Assert.NotEmpty(fixture.CompiledRelease.WorkItems);
    }

    [Fact]
    public async Task GetById_ReadsStoredReleaseWithAndWithoutProjectId()
    {
        Assert.Equal(HttpStatusCode.Accepted, fixture.CompileStatus);

        var documentWithProjectId = await fixture.Client.GetFromJsonAsync<ReleaseNoteDocument>(
            $"releases/{Uri.EscapeDataString(fixture.ExpectedId)}?projectId={Uri.EscapeDataString(fixture.Settings.ProjectId)}",
            CompiledReleaseFixture.JsonOptions);
        var documentWithoutProjectId = await fixture.Client.GetFromJsonAsync<ReleaseNoteDocument>(
            $"releases/{Uri.EscapeDataString(fixture.ExpectedId)}",
            CompiledReleaseFixture.JsonOptions);

        Assert.NotNull(documentWithProjectId);
        Assert.NotNull(documentWithoutProjectId);
        Assert.Equal(fixture.ExpectedId, documentWithProjectId.Id);
        Assert.Equal(fixture.Settings.BuildId, documentWithProjectId.BuildId);
        Assert.Equal(fixture.CompiledRelease.BuildNumber, documentWithProjectId.BuildNumber);
        Assert.Equal(fixture.CompiledRelease.Commits.Select(commit => commit.Id), documentWithProjectId.Commits.Select(commit => commit.Id));
        Assert.Equal(fixture.CompiledRelease.WorkItems.Select(item => item.Id), documentWithProjectId.WorkItems.Select(item => item.Id));
        Assert.Equal(
            JsonSerializer.Serialize(documentWithProjectId, CompiledReleaseFixture.JsonOptions),
            JsonSerializer.Serialize(documentWithoutProjectId, CompiledReleaseFixture.JsonOptions));
        Assert.NotEmpty(documentWithProjectId.Commits);
        Assert.NotEmpty(documentWithProjectId.WorkItems);
    }

    [Fact]
    public async Task Recompile_IsAcceptedAndProjectListRemainsUnique()
    {
        Assert.Equal(HttpStatusCode.Accepted, fixture.CompileStatus);
        var (status, body, _) = await fixture.CompileAsync();
        Assert.True(status == HttpStatusCode.Accepted, $"Second compile returned HTTP {(int)status}: {body}");

        var documents = await fixture.Client.GetFromJsonAsync<List<ReleaseNoteDocument>>(
            $"releases?project={Uri.EscapeDataString(fixture.Settings.Project)}",
            CompiledReleaseFixture.JsonOptions);

        Assert.NotNull(documents);
        var listedRelease = Assert.Single(documents, document => document.Id == fixture.ExpectedId);
        Assert.Equal(fixture.CompiledRelease.BuildNumber, listedRelease.BuildNumber);
        Assert.Equal(fixture.CompiledRelease.Commits.Select(commit => commit.Id), listedRelease.Commits.Select(commit => commit.Id));
        Assert.Equal(fixture.CompiledRelease.WorkItems.Select(item => item.Id), listedRelease.WorkItems.Select(item => item.Id));
    }

    [Fact]
    public async Task Health_ReportsTableStorageAndAzureDevOpsHealthy()
    {
        Assert.Equal(HttpStatusCode.Accepted, fixture.CompileStatus);

        using var response = await fixture.Client.GetAsync("health");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Health returned HTTP {(int)response.StatusCode}: {body}");

        var health = JsonSerializer.Deserialize<HealthResult>(body, CompiledReleaseFixture.JsonOptions)!;
        Assert.Equal("healthy", health.Status);
        var connections = health.Connections.ToDictionary(connection => connection.Name, connection => connection.Status);
        Assert.Equal("healthy", connections["tableStorage"]);
        Assert.Equal("healthy", connections["azureDevOps"]);
    }

    private sealed record HealthResult(string Status, List<ConnectionResult> Connections);

    private sealed record ConnectionResult(string Name, string Status);
}
