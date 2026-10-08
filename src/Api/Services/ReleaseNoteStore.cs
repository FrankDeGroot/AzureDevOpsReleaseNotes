using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Azure.Cosmos;
using Shared;

namespace Api.Services;

public interface IReleaseNoteStore
{
    Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken);
    Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken);
}

public sealed class CosmosReleaseNoteStore : IReleaseNoteStore, IExternalConnectionHealthCheck
{
    private readonly CosmosClient client;
    private readonly string databaseName;
    private readonly string containerName;

    public CosmosReleaseNoteStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration["Cosmos:ConnectionString"] ?? "https://localhost:8081/";
        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };

        if (environment.IsDevelopment() && HasLoopbackAccountEndpoint(connectionString))
        {
            clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = ValidateLocalEmulatorCertificate
            });
        }

        client = new CosmosClient(connectionString, clientOptions);
        databaseName = configuration["Cosmos:Database"] ?? "release-notes";
        containerName = configuration["Cosmos:Container"] ?? "releases";
    }

    public string Name => "cosmosDb";

    public async Task<string> CheckAsync(CancellationToken cancellationToken)
    {
        await client.GetContainer(databaseName, containerName).ReadContainerAsync(cancellationToken: cancellationToken);
        return "Database and container reachable.";
    }

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
        if (string.IsNullOrWhiteSpace(projectId))
        {
            using var iterator = container.GetItemQueryIterator<ReleaseNoteDocument>(
                new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.id = @id").WithParameter("@id", id));
            var results = await iterator.ReadNextAsync(cancellationToken);
            return results.Resource.FirstOrDefault();
        }

        try
        {
            var response = await container.ReadItemAsync<ReleaseNoteDocument>(id, new PartitionKey(projectId), cancellationToken: cancellationToken);
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

    private static bool HasLoopbackAccountEndpoint(string connectionString)
    {
        var endpointSetting = connectionString.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase)
            ? connectionString["AccountEndpoint=".Length..].Split(';')[0]
            : connectionString;
        return Uri.TryCreate(endpointSetting, UriKind.Absolute, out var endpoint) && endpoint.IsLoopback;
    }

    private static bool ValidateLocalEmulatorCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        return errors == SslPolicyErrors.RemoteCertificateChainErrors
            && request.RequestUri?.IsLoopback == true
            && certificate?.GetNameInfo(X509NameType.DnsName, false) == "localhost"
            && chain?.ChainStatus.Length > 0
            && chain.ChainStatus.All(status => status.Status == X509ChainStatusFlags.UntrustedRoot);
    }
}