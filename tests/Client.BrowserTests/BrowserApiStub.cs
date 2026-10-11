using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Shared;

namespace Client.BrowserTests;

internal sealed class BrowserApiStub
{
    internal const string ReleaseId = "browser-test-release";

    private static readonly ReleaseNoteDocument Release = new()
    {
        Id = ReleaseId,
        Project = "Browser Test Project",
        ProjectId = "browser-test-project-id",
        RepositoryId = "browser-test-repository-id",
        BuildId = 20260918,
        BuildNumber = "browser-test-2026.09.18.1",
        SourceBranch = "refs/heads/main",
        BuildDate = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
        BuildUri = "https://example.test/builds/20260918",
        WorkItems =
        [
            new WorkItemSummary
            {
                Id = 842,
                Title = "Validate browser release view",
                State = "Done",
                Type = "Task",
                Url = "https://example.test/workitems/842"
            }
        ],
        Commits =
        [
            new CommitSummary
            {
                Id = "abc123",
                Message = "Add seeded browser release data",
                Author = "Release Notes CI",
                Timestamp = new DateTime(2026, 9, 18, 11, 30, 0, DateTimeKind.Utc),
                Url = "https://example.test/commits/abc123"
            }
        ]
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentQueue<string> requestedPaths = new();

    internal IReadOnlyCollection<string> RequestedPaths => requestedPaths.ToArray();

    internal Task InstallAsync(IPage page) => page.RouteAsync(
        new Regex(@".*/api/releases(?:/[^?]*)?(?:\?.*)?$"),
        HandleAsync);

    private async Task HandleAsync(IRoute route)
    {
        var request = route.Request;
        var path = new Uri(request.Url).AbsolutePath;
        requestedPaths.Enqueue(path);

        if (request.Method != "GET")
        {
            await route.AbortAsync();
            return;
        }

        if (path == "/api/releases")
        {
            await FulfillAsync(route, new[] { Release });
            return;
        }

        if (path == $"/api/releases/{Uri.EscapeDataString(ReleaseId)}")
        {
            await FulfillAsync(route, Release);
            return;
        }

        await route.FulfillAsync(new RouteFulfillOptions { Status = 404, Body = "Release not found" });
    }

    private static Task FulfillAsync<T>(IRoute route, T value) => route.FulfillAsync(new RouteFulfillOptions
    {
        Status = 200,
        ContentType = "application/json",
        Body = JsonSerializer.Serialize(value, JsonOptions)
    });
}