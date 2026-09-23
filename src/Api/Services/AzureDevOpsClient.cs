using System.Net.Http.Headers;
using System.Text.Json;
using Shared;

namespace Api.Services;

public sealed class AzureDevOpsClient(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<AzureBuild> GetBuildAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, $"https://dev.azure.com/{Uri.EscapeDataString(request.Organization)}/{Uri.EscapeDataString(request.Project)}/_apis/build/builds/{request.BuildId}?api-version=7.1", cancellationToken);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        return new AzureBuild(root.GetProperty("id").GetInt32(), root.GetProperty("buildNumber").GetString() ?? request.BuildId.ToString(), root.TryGetProperty("sourceBranch", out var branch) ? branch.GetString() ?? string.Empty : string.Empty, root.TryGetProperty("startTime", out var start) && start.ValueKind == JsonValueKind.String ? start.GetDateTime() : DateTime.UtcNow, root.TryGetProperty("uri", out var uri) ? uri.GetString() ?? string.Empty : string.Empty);
    }

    public async Task<IReadOnlyList<CommitSummary>> GetChangesAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, $"https://dev.azure.com/{Uri.EscapeDataString(request.Organization)}/{Uri.EscapeDataString(request.Project)}/_apis/build/builds/{request.BuildId}/changes?top=1000&api-version=7.1", cancellationToken);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("value").EnumerateArray().Select(change => new CommitSummary
        {
            Id = change.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            Message = change.TryGetProperty("message", out var message) ? message.GetString() ?? string.Empty : string.Empty,
            Author = change.TryGetProperty("author", out var author) && author.TryGetProperty("displayName", out var displayName) ? displayName.GetString() ?? string.Empty : string.Empty,
            Timestamp = change.TryGetProperty("timestamp", out var timestamp) && timestamp.ValueKind == JsonValueKind.String ? timestamp.GetDateTime() : DateTime.UtcNow,
            Url = change.TryGetProperty("location", out var location) ? location.GetString() ?? string.Empty : string.Empty
        }).ToArray();
    }

    public async Task<IReadOnlyList<WorkItemSummary>> GetWorkItemsAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, $"https://dev.azure.com/{Uri.EscapeDataString(request.Organization)}/{Uri.EscapeDataString(request.Project)}/_apis/build/builds/{request.BuildId}/workitems?api-version=7.1", cancellationToken);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var ids = document.RootElement.GetProperty("value").EnumerateArray().Select(item => item.TryGetProperty("id", out var id) && id.TryGetInt32(out var value) ? value : 0).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return [];
        }

        using var detailsResponse = await SendAsync(request, $"https://dev.azure.com/{Uri.EscapeDataString(request.Organization)}/{Uri.EscapeDataString(request.Project)}/_apis/wit/workitems?ids={string.Join(',', ids)}&api-version=7.1", cancellationToken);
        using var details = await JsonDocument.ParseAsync(await detailsResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return details.RootElement.GetProperty("value").EnumerateArray().Select(item =>
        {
            var fields = item.GetProperty("fields");
            return new WorkItemSummary
            {
                Id = item.GetProperty("id").GetInt32(),
                Title = GetField(fields, "System.Title"),
                State = GetField(fields, "System.State"),
                Type = GetField(fields, "System.WorkItemType"),
                Url = item.TryGetProperty("_links", out var links) && links.TryGetProperty("html", out var html) ? html.GetProperty("href").GetString() ?? string.Empty : string.Empty
            };
        }).ToArray();
    }

    private async Task<HttpResponseMessage> SendAsync(CompileRequest request, string url, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        var token = string.IsNullOrWhiteSpace(request.AccessToken) ? configuration["AzureDevOps:AccessToken"] : request.AccessToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    private static string GetField(JsonElement fields, string name) => fields.TryGetProperty(name, out var field) ? field.GetString() ?? string.Empty : string.Empty;
}

public sealed record AzureBuild(int Id, string BuildNumber, string SourceBranch, DateTime BuildDate, string Uri);