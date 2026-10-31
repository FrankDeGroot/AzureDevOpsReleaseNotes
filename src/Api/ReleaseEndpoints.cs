using System.Text.Json;
using Api.Services;
using Shared;

namespace Api;

public static class ReleaseEndpoints
{
    public static void MapReleaseEndpoints(this WebApplication app)
    {
        app.MapPost("/api/releases/compile", CompileAsync).RequireAuthorization("Compile");
        app.MapGet("/api/releases", async (HttpRequest request, IReleaseNoteStore store, CancellationToken cancellationToken) =>
            TypedResults.Ok(await store.ListAsync(request.Query["project"].FirstOrDefault(), cancellationToken)));
        app.MapGet("/api/releases/{id}", GetAsync);
    }

    private static async Task<IResult> CompileAsync(HttpRequest request, ReleaseNoteCompiler compiler, CancellationToken cancellationToken)
    {
        CompileRequest? input;
        try
        {
            input = await JsonSerializer.DeserializeAsync<CompileRequest>(request.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken);
        }
        catch (JsonException)
        {
            return TypedResults.BadRequest(new { error = "Request body must be valid JSON." });
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Organization) || string.IsNullOrWhiteSpace(input.Project) ||
            input.BuildId <= 0 || string.IsNullOrWhiteSpace(input.RepositoryId))
        {
            return TypedResults.BadRequest(new { error = "organization, project, buildId, and repositoryId are required." });
        }

        input.ProjectId = string.IsNullOrWhiteSpace(input.ProjectId) ? input.Project : input.ProjectId;
        return TypedResults.Ok(await compiler.CompileAsync(input, cancellationToken));
    }

    private static async Task<IResult> GetAsync(string id, HttpRequest request, IReleaseNoteStore store, CancellationToken cancellationToken)
    {
        var document = await store.GetAsync(id, request.Query["projectId"].FirstOrDefault(), cancellationToken);
        return document is null
            ? TypedResults.NotFound(new { error = "Release note was not found." })
            : TypedResults.Ok(document);
    }
}