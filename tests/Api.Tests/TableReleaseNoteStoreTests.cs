using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using Azure.Identity;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Shared;

namespace Api.Tests;

public sealed class TableReleaseNoteStoreTests
{
    [AzuriteFact]
    public async Task CheckAsync_connects_to_local_Azurite_table_storage()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:ConnectionString"] = "UseDevelopmentStorage=true",
                ["Storage:TableName"] = "releases"
            })
            .Build();
        var store = new TableReleaseNoteStore(configuration, new DefaultAzureCredential());

        var description = await store.CheckAsync(CancellationToken.None);

        Assert.Equal("Storage table reachable.", description);
    }

    [Fact]
    public async Task CheckAsync_creates_and_queries_the_table()
    {
        var client = CreateTableClient();
        var store = new TableReleaseNoteStore(client.Object);

        var description = await store.CheckAsync(CancellationToken.None);

        Assert.Equal("Storage table reachable.", description);
        client.Verify(table => table.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(table => table.QueryAsync<TableEntity>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertAsync_uses_project_and_build_keys_and_preserves_nested_data()
    {
        var client = CreateTableClient();
        TableEntity? persisted = null;
        client.Setup(table => table.UpsertEntityAsync(
                It.IsAny<TableEntity>(),
                TableUpdateMode.Replace,
                It.IsAny<CancellationToken>()))
            .Callback<TableEntity, TableUpdateMode, CancellationToken>((entity, _, _) => persisted = entity)
            .ReturnsAsync((Response)null!);
        var document = CreateDocument("project-id", 42, "Release Project", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));

        var result = await new TableReleaseNoteStore(client.Object).UpsertAsync(document, CancellationToken.None);

        Assert.Same(document, result);
        Assert.NotNull(persisted);
        Assert.Equal("project-id", persisted.PartitionKey);
        Assert.Equal("42", persisted.RowKey);
        Assert.Equal("project-id:42", persisted["id"]);
        Assert.Equal(123, Assert.Single(System.Text.Json.JsonSerializer.Deserialize<List<WorkItemSummary>>((string)persisted["workItemsJson"]!)!).Id);
        Assert.Equal("commit-1", Assert.Single(System.Text.Json.JsonSerializer.Deserialize<List<CommitSummary>>((string)persisted["commitsJson"]!)!).Id);
    }

    [Theory]
    [InlineData("project-id")]
    [InlineData(null)]
    public async Task GetAsync_reads_using_project_and_build_keys_and_maps_nested_data(string? projectId)
    {
        var client = CreateTableClient();
        client.Setup(table => table.GetEntityAsync<TableEntity>(
                "project-id",
                "42",
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(CreateEntity("project-id", 42, "Release Project", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)), Mock.Of<Response>()));

        var result = await new TableReleaseNoteStore(client.Object).GetAsync("project-id:42", projectId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("project-id:42", result.Id);
        Assert.Equal("Release Project", result.Project);
        Assert.Equal(123, Assert.Single(result.WorkItems).Id);
        Assert.Equal("commit-1", Assert.Single(result.Commits).Id);
        client.Verify(table => table.GetEntityAsync<TableEntity>("project-id", "42", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_project_id_does_not_match_document_id()
    {
        var client = CreateTableClient();

        var result = await new TableReleaseNoteStore(client.Object).GetAsync("project-id:42", "another-project", CancellationToken.None);

        Assert.Null(result);
        client.Verify(table => table.GetEntityAsync<TableEntity>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListAsync_filters_by_project_and_orders_by_build_date_descending()
    {
        var client = CreateTableClient(
        [
            CreateEntity("project-id", 41, "Release Project", new DateTime(2026, 1, 1, 3, 4, 5, DateTimeKind.Utc)),
            CreateEntity("project-id", 42, "Release Project", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
        ]);
        string? filter = null;
        client.Setup(table => table.QueryAsync<TableEntity>(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, int?, IEnumerable<string>, CancellationToken>((value, _, _, _) => filter = value)
            .Returns(CreatePageable(
            [
                CreateEntity("project-id", 41, "Release Project", new DateTime(2026, 1, 1, 3, 4, 5, DateTimeKind.Utc)),
                CreateEntity("project-id", 42, "Release Project", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc))
            ]));

        var results = await new TableReleaseNoteStore(client.Object).ListAsync("Release Project", CancellationToken.None);

        Assert.Equal("project eq 'Release Project'", filter);
        Assert.Equal([42, 41], results.Select(document => document.BuildId));
    }

    [Fact]
    public async Task ListAsync_without_project_queries_all_records_and_orders_by_build_date_descending()
    {
        var client = CreateTableClient();
        client.Setup(table => table.QueryAsync<TableEntity>(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreatePageable(
            [
                CreateEntity("project-id", 41, "Release Project", new DateTime(2026, 1, 1, 3, 4, 5, DateTimeKind.Utc)),
                CreateEntity("other-project", 50, "Other Project", new DateTime(2026, 1, 3, 3, 4, 5, DateTimeKind.Utc))
            ]));

        var results = await new TableReleaseNoteStore(client.Object).ListAsync(null, CancellationToken.None);

        Assert.Equal([50, 41], results.Select(document => document.BuildId));
        client.Verify(table => table.QueryAsync<TableEntity>(It.Is<string?>(filter => filter == null), It.IsAny<int?>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<TableClient> CreateTableClient(IEnumerable<TableEntity>? entities = null)
    {
        var client = new Mock<TableClient>();
        client.Setup(table => table.CreateIfNotExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<TableItem>)null!);
        client.Setup(table => table.QueryAsync<TableEntity>(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreatePageable(entities ?? []));
        return client;
    }

    private static AsyncPageable<TableEntity> CreatePageable(IEnumerable<TableEntity> entities) =>
        AsyncPageable<TableEntity>.FromPages([Page<TableEntity>.FromValues(entities.ToList(), null, Mock.Of<Response>())]);

    private static TableEntity CreateEntity(string projectId, int buildId, string project, DateTime buildDate)
    {
        var document = CreateDocument(projectId, buildId, project, buildDate);
        return new TableEntity(projectId, buildId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            ["id"] = document.Id,
            ["project"] = document.Project,
            ["projectId"] = document.ProjectId,
            ["repositoryId"] = document.RepositoryId,
            ["buildId"] = document.BuildId,
            ["buildNumber"] = document.BuildNumber,
            ["sourceBranch"] = document.SourceBranch,
            ["buildDate"] = new DateTimeOffset(document.BuildDate),
            ["buildUri"] = document.BuildUri,
            ["workItemsJson"] = System.Text.Json.JsonSerializer.Serialize(document.WorkItems),
            ["commitsJson"] = System.Text.Json.JsonSerializer.Serialize(document.Commits)
        };
    }

    private static ReleaseNoteDocument CreateDocument(string projectId, int buildId, string project, DateTime buildDate) => new()
    {
        Id = $"{projectId}:{buildId}",
        Project = project,
        ProjectId = projectId,
        RepositoryId = "repository-id",
        BuildId = buildId,
        BuildNumber = $"build-{buildId}",
        SourceBranch = "refs/heads/main",
        BuildDate = buildDate,
        BuildUri = $"https://dev.azure.com/example/builds/{buildId}",
        WorkItems = [new WorkItemSummary { Id = 123, Title = "Add feature", State = "Done", Type = "Task", Url = "https://dev.azure.com/example/workitems/123" }],
        Commits = [new CommitSummary { Id = "commit-1", Message = "Add feature", Author = "Ada", Timestamp = buildDate, Url = "https://dev.azure.com/example/commit/commit-1" }]
    };
}

internal sealed class AzuriteFactAttribute : FactAttribute
{
    public AzuriteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_AZURITE_INTEGRATION_TESTS") != "1")
        {
            Skip = "Set RUN_AZURITE_INTEGRATION_TESTS=1 when Azurite is available.";
        }
    }
}