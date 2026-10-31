using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Data.Tables;
using Shared;

namespace Api.Services;

public interface IReleaseNoteStore
{
    Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken);
    Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken);
}

public sealed class TableReleaseNoteStore : IReleaseNoteStore, IExternalConnectionHealthCheck
{
    private const int MaxStringPropertyBytes = 64 * 1024;
    private const int MaxEntityBytes = 1024 * 1024;
    private readonly TableClient tableClient;

    public TableReleaseNoteStore(IConfiguration configuration, TokenCredential credential)
        : this(CreateTableClient(configuration, credential))
    {
    }

    public TableReleaseNoteStore(TableClient tableClient)
    {
        this.tableClient = tableClient;
    }

    private static TableClient CreateTableClient(IConfiguration configuration, TokenCredential credential)
    {
        var connectionString = configuration["Storage:ConnectionString"];
        var serviceClient = !string.IsNullOrWhiteSpace(connectionString)
            ? new TableServiceClient(connectionString)
            : new TableServiceClient(
                new Uri(configuration["Storage:TableEndpoint"] ?? throw new InvalidOperationException("Storage:TableEndpoint is required when Storage:ConnectionString is not configured.")),
                credential);
            return serviceClient.GetTableClient(configuration["Storage:TableName"] ?? "releases");
    }

    public string Name => "tableStorage";

    public async Task<string> CheckAsync(CancellationToken cancellationToken)
    {
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
        await foreach (var _ in tableClient.QueryAsync<TableEntity>(maxPerPage: 1, cancellationToken: cancellationToken))
        {
            break;
        }

        return "Storage table reachable.";
    }

    public async Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken)
    {
        var entity = ToEntity(document);
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
        return document;
    }

    public async Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken)
    {
        await tableClient.CreateIfNotExistsAsync(cancellationToken);
        var filter = string.IsNullOrWhiteSpace(project)
            ? null
            : TableClient.CreateQueryFilter($"project eq {project}");
        var results = new List<ReleaseNoteDocument>();
        await foreach (var entity in tableClient.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken))
        {
            results.Add(FromEntity(entity));
        }

        return results.OrderByDescending(document => document.BuildDate).ToArray();
    }

    public async Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken)
    {
        if (!TryParseId(id, out var idProject, out var rowKey))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(projectId) && !string.Equals(projectId, idProject, StringComparison.Ordinal))
        {
            return null;
        }

        var partitionKey = string.IsNullOrWhiteSpace(projectId) ? idProject : projectId;
        try
        {
            var response = await tableClient.GetEntityAsync<TableEntity>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return FromEntity(response.Value);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }

    private static TableEntity ToEntity(ReleaseNoteDocument document)
    {
        var entity = new TableEntity(document.ProjectId, document.BuildId.ToString(CultureInfo.InvariantCulture))
        {
            ["id"] = document.Id,
            ["project"] = document.Project,
            ["projectId"] = document.ProjectId,
            ["repositoryId"] = document.RepositoryId,
            ["buildId"] = document.BuildId,
            ["buildNumber"] = document.BuildNumber,
            ["sourceBranch"] = document.SourceBranch,
            ["buildDate"] = new DateTimeOffset(document.BuildDate.ToUniversalTime()),
            ["buildUri"] = document.BuildUri,
            ["workItemsJson"] = JsonSerializer.Serialize(document.WorkItems),
            ["commitsJson"] = JsonSerializer.Serialize(document.Commits)
        };

        ValidateEntitySize(entity);
        return entity;
    }

    private static ReleaseNoteDocument FromEntity(TableEntity entity)
    {
        var buildDate = entity.TryGetValue("buildDate", out var value) && value is DateTimeOffset dateTimeOffset
            ? dateTimeOffset.UtcDateTime
            : DateTime.MinValue;

        return new ReleaseNoteDocument
        {
            Id = GetString(entity, "id"),
            Project = GetString(entity, "project"),
            ProjectId = GetString(entity, "projectId"),
            RepositoryId = GetString(entity, "repositoryId"),
            BuildId = GetInt32(entity, "buildId"),
            BuildNumber = GetString(entity, "buildNumber"),
            SourceBranch = GetString(entity, "sourceBranch"),
            BuildDate = buildDate,
            BuildUri = GetString(entity, "buildUri"),
            WorkItems = DeserializeList<WorkItemSummary>(GetString(entity, "workItemsJson")),
            Commits = DeserializeList<CommitSummary>(GetString(entity, "commitsJson"))
        };
    }

    private static bool TryParseId(string id, out string projectId, out string rowKey)
    {
        var separatorIndex = id.LastIndexOf(':');
        projectId = separatorIndex > 0 ? id[..separatorIndex] : string.Empty;
        var buildIdText = separatorIndex >= 0 ? id[(separatorIndex + 1)..] : string.Empty;
        if (projectId.Length == 0 || !int.TryParse(buildIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var buildId))
        {
            rowKey = string.Empty;
            return false;
        }

        rowKey = buildId.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static void ValidateEntitySize(TableEntity entity)
    {
        var estimatedBytes = 512;
        foreach (var (name, value) in entity)
        {
            var propertyBytes = value is string text
                ? Math.Max(Encoding.UTF8.GetByteCount(text), Encoding.Unicode.GetByteCount(text))
                : 16;
            if (value is string && propertyBytes > MaxStringPropertyBytes)
            {
                throw new InvalidOperationException($"Release note property '{name}' exceeds the Azure Table Storage 64 KiB string property limit.");
            }

            estimatedBytes += Encoding.UTF8.GetByteCount(name) + propertyBytes + 64;
        }

        estimatedBytes += Encoding.UTF8.GetByteCount(entity.PartitionKey) + Encoding.UTF8.GetByteCount(entity.RowKey);
        if (estimatedBytes > MaxEntityBytes)
        {
            throw new InvalidOperationException("Release note exceeds the Azure Table Storage 1 MiB entity limit.");
        }
    }

    private static string GetString(TableEntity entity, string key) =>
        entity.TryGetValue(key, out var value) && value is string text ? text : string.Empty;

    private static int GetInt32(TableEntity entity, string key) =>
        entity.TryGetValue(key, out var value) && value is int integer ? integer : 0;

    private static List<T> DeserializeList<T>(string value) =>
        string.IsNullOrEmpty(value) ? [] : JsonSerializer.Deserialize<List<T>>(value) ?? [];
}