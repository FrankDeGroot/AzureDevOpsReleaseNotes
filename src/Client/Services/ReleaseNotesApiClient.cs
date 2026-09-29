using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared;

namespace Client.Services;

public sealed class ReleaseNotesApiClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project = null, CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(project) ? "api/releases" : $"api/releases?project={Uri.EscapeDataString(project)}";
        return await httpClient.GetFromJsonAsync<List<ReleaseNoteDocument>>(path, cancellationToken) ?? [];
    }

    public async Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(projectId) ? $"api/releases/{Uri.EscapeDataString(id)}" : $"api/releases/{Uri.EscapeDataString(id)}?projectId={Uri.EscapeDataString(projectId)}";
            return await httpClient.GetFromJsonAsync<ReleaseNoteDocument>(path, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return null;
        }
    }
}

public sealed class ReleaseNotesState(ReleaseNotesApiClient apiClient, ILogger<ReleaseNotesState> logger)
{
    public IReadOnlyList<ReleaseNoteDocument> Releases { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public string? Error { get; private set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        Error = null;
        try
        {
            Releases = await apiClient.ListAsync(cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            Error = exception.Message;
            logger.LogError(exception, "Failed to load release notes.");
        }
        finally
        {
            IsLoading = false;
        }
    }
}