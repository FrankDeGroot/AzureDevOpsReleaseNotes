using Microsoft.Azure.Cosmos;
using Shared;

namespace Api.Services;

public interface IReleaseNoteStore
{
    Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken);
    Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken);
}

public sealed class CosmosReleaseNoteStore(IConfiguration configuration) : IReleaseNoteStore
{
    private readonly CosmosClient client = new(configuration["Cosmos:ConnectionString"] ?? "https://localhost:8081/", new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });
    private readonly string databaseName = configuration["Cosmos:Database"] ?? "release-notes";
    private readonly string containerName = configuration["Cosmos:Container"] ?? "releases";

    public async Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        var response = await container.UpsertItemAsync(document, new PartitionKey(document.ProjectId), cancellationToken: cancellationToken);
        return response.Resource;
    }

    public async Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        var query = string.IsNullOrWhiteSpace(project) ? new QueryDefinition("SELECT * FROM c ORDER BY c.buildDate DESC") : new QueryDefinition("SELECT * FROM c WHERE c.project = @project ORDER BY c.buildDate DESC").WithParameter("@project", project);
        var results = new List<ReleaseNoteDocument>();
        using var iterator = container.GetItemQueryIterator<ReleaseNoteDocument>(query);
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync(cancellationToken));
        }

        return results;
    }

    public async Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        try
        {
            var response = await container.ReadItemAsync<ReleaseNoteDocument>(id, new PartitionKey(projectId ?? string.Empty), cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<Container> GetContainerAsync(CancellationToken cancellationToken)
    {
        var database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken);
        var container = await database.Database.CreateContainerIfNotExistsAsync(containerName, "/projectId", cancellationToken: cancellationToken);
        return container.Container;
    }
}