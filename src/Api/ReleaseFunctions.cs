using System.Net;
using System.Text.Json;
using Azure;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared;

namespace Api;

public sealed class ReleaseFunctions(
    ICompilationQueueSubmission queueSubmission,
    IReleaseNoteStore store,
    ILogger<ReleaseFunctions> logger)
{
    [Function("CompileRelease")]
    public async Task<HttpResponseData> Compile(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "releases/compile")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        CompileRequest? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<CompileRequest>(
                request.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);
        }
        catch (JsonException)
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, "Request body must be valid JSON.");
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Organization) || string.IsNullOrWhiteSpace(input.Project) || input.BuildId <= 0 || string.IsNullOrWhiteSpace(input.RepositoryId))
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, "organization, project, buildId, and repositoryId are required.");
        }

        input.ProjectId = string.IsNullOrWhiteSpace(input.ProjectId) ? input.Project : input.ProjectId;
        var id = $"{input.ProjectId}:{input.BuildId}";
        var releaseUrl = $"/api/releases/{Uri.EscapeDataString(id)}?projectId={Uri.EscapeDataString(input.ProjectId)}";
        try
        {
            await queueSubmission.SubmitAsync(input, cancellationToken);
        }
        catch (CompilationQueueMessageTooLargeException exception)
        {
            return await ErrorAsync(request, HttpStatusCode.BadRequest, exception.Message);
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(exception, "Failed to queue release compilation {ReleaseId}.", id);
            return await ErrorAsync(request, HttpStatusCode.ServiceUnavailable, "The compilation request could not be queued. Please retry.");
        }

        var response = request.CreateResponse(HttpStatusCode.Accepted);
        response.Headers.Add("Location", releaseUrl);
        await response.WriteAsJsonAsync(new CompileReleaseAcknowledgment
        {
            Id = id,
            ProjectId = input.ProjectId,
            ReleaseUrl = releaseUrl
        }, cancellationToken);
        return response;
    }

    [Function("ListReleases")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "releases")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(await store.ListAsync(GetQuery(request.Url, "project"), cancellationToken), cancellationToken);
        return response;
    }

    [Function("GetRelease")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "releases/{id}")] HttpRequestData request,
        string id,
        CancellationToken cancellationToken)
    {
        var document = await store.GetAsync(id, GetQuery(request.Url, "projectId"), cancellationToken);
        if (document is null)
        {
            return await ErrorAsync(request, HttpStatusCode.NotFound, "Release note was not found.");
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(document, cancellationToken);
        return response;
    }

    private static string? GetQuery(Uri uri, string key)
    {
        var pair = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Split('=', 2))
            .FirstOrDefault(value => value.Length == 2 && string.Equals(Uri.UnescapeDataString(value[0]), key, StringComparison.OrdinalIgnoreCase));
        return pair is null ? null : Uri.UnescapeDataString(pair[1]);
    }

    private static async Task<HttpResponseData> ErrorAsync(HttpRequestData request, HttpStatusCode status, string message)
    {
        var response = request.CreateResponse(status);
        await response.WriteAsJsonAsync(new { error = message });
        return response;
    }
}