using System.Net;
using System.Text;
using System.Text.Json;
using Shared;

namespace Api.IntegrationTests;

public sealed class ReleasePollingClientTests
{
    private const string ReleaseId = "project-id:42";
    private static readonly Uri ReleaseUrl = new("https://example.test/api/releases/project-id%3A42?projectId=project-id");

    [Fact]
    public async Task PollAsync_retries_404_until_release_is_available()
    {
        var clock = new ManualTimeProvider();
        var requests = 0;
        using var client = CreateClient(new StubHandler(_ => ++requests < 3
            ? JsonResponse(HttpStatusCode.NotFound, "not found")
            : JsonResponse(HttpStatusCode.OK, SerializeRelease())));
        var poller = CreatePoller(client, clock);

        var result = await poller.PollAsync(ReleaseUrl, ReleaseId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

        Assert.Equal(ReleaseId, result.Id);
        Assert.Equal(3, requests);
        Assert.Equal(TimeSpan.FromSeconds(2), clock.GetElapsedTime(0));
    }

    [Fact]
    public async Task PollAsync_enforces_one_overall_deadline_and_reports_last_response()
    {
        var clock = new ManualTimeProvider();
        var requests = 0;
        using var client = CreateClient(new StubHandler(_ =>
        {
            requests++;
            return JsonResponse(HttpStatusCode.NotFound, "still pending");
        }));
        var poller = CreatePoller(client, clock);

        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            poller.PollAsync(ReleaseUrl, ReleaseId, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));

        Assert.Equal(2, requests);
        Assert.Contains(ReleaseId, error.Message, StringComparison.Ordinal);
        Assert.Contains("HTTP 404: still pending", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollAsync_cancels_a_read_at_the_remaining_deadline()
    {
        using var client = CreateClient(new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return JsonResponse(HttpStatusCode.OK, SerializeRelease());
        }));
        var poller = new ReleasePollingClient(client);

        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            poller.PollAsync(ReleaseUrl, ReleaseId, TimeSpan.FromMilliseconds(30), TimeSpan.FromSeconds(1)));

        Assert.Contains(ReleaseId, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PollAsync_fails_immediately_on_non_404_http_status()
    {
        var requests = 0;
        using var client = CreateClient(new StubHandler(_ =>
        {
            requests++;
            return JsonResponse(HttpStatusCode.ServiceUnavailable, "storage unavailable");
        }));
        var poller = CreatePoller(client, new ManualTimeProvider());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            poller.PollAsync(ReleaseUrl, ReleaseId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Contains("storage unavailable", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task PollAsync_fails_immediately_on_invalid_release_json()
    {
        using var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{")));
        var poller = CreatePoller(client, new ManualTimeProvider());

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            poller.PollAsync(ReleaseUrl, ReleaseId, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task EnsureAbsentAsync_rejects_a_cached_release_with_fresh_build_guidance()
    {
        using var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, SerializeRelease())));
        var poller = new ReleasePollingClient(client);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            poller.EnsureAbsentAsync(ReleaseUrl, ReleaseId, CancellationToken.None));

        Assert.Contains(ReleaseId, error.Message, StringComparison.Ordinal);
        Assert.Contains("-NewBuild", error.Message, StringComparison.Ordinal);
    }

    private static ReleasePollingClient CreatePoller(HttpClient client, ManualTimeProvider clock) =>
        new(client, clock, (duration, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            clock.Advance(duration);
            return Task.CompletedTask;
        });

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string SerializeRelease() => JsonSerializer.Serialize(new ReleaseNoteDocument
    {
        Id = ReleaseId,
        Project = "Release Project",
        ProjectId = "project-id",
        BuildId = 42,
        BuildNumber = "build-42"
    }, CompiledReleaseFixture.JsonOptions);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;

        public void Advance(TimeSpan duration) => timestamp += duration.Ticks;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
            : this(request => responseFactory(request, CancellationToken.None).GetAwaiter().GetResult())
        {
            this.asyncResponseFactory = responseFactory;
        }

        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? asyncResponseFactory;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            asyncResponseFactory is null
                ? Task.FromResult(responseFactory(request))
                : asyncResponseFactory(request, cancellationToken);
    }
}