using System.Net;
using System.Text;
using System.Text.Json;
using Api;
using Api.Services;
using Azure.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared;

namespace Api.Tests;

public sealed class CompileReleaseQueueFunctionTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ProcessAsync_compiles_and_persists_release()
    {
        var store = new InMemoryReleaseNoteStore();
        var function = CreateFunction(store, new StubHandler(CreateSuccessfulResponse));

        await function.ProcessAsync(SerializeEnvelope(), CancellationToken.None);

        var document = Assert.Single(store.Documents);
        Assert.Equal("project-id:42", document.Id);
        Assert.Equal("build-42", document.BuildNumber);
        Assert.Equal("commit-1", Assert.Single(document.Commits).Id);
        Assert.Equal(123, Assert.Single(document.WorkItems).Id);
    }

    [Fact]
    public async Task ProcessAsync_duplicate_delivery_upserts_one_release_record()
    {
        var store = new InMemoryReleaseNoteStore();
        var function = CreateFunction(store, new StubHandler(CreateSuccessfulResponse));
        var message = SerializeEnvelope();

        await function.ProcessAsync(message, CancellationToken.None);
        await function.ProcessAsync(message, CancellationToken.None);

        Assert.Equal(2, store.UpsertCount);
        Assert.Single(store.Documents);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"version":2,"correlationId":"4a33a6f2-4c6e-451f-82c8-bbf996806034","request":{}}""")]
    [InlineData("""{"version":1,"correlationId":"not-a-guid","request":{"organization":"contoso","project":"release-notes","projectId":"project-id","buildId":42,"repositoryId":"repo-id"}}""")]
    [InlineData("""{"version":1,"correlationId":"4a33a6f2-4c6e-451f-82c8-bbf996806034","request":{"organization":"contoso","project":"release-notes","projectId":"project-id","buildId":0,"repositoryId":"repo-id"}}""")]
    public async Task ProcessAsync_rejects_malformed_unsupported_or_invalid_messages(string message)
    {
        var function = CreateFunction(new InMemoryReleaseNoteStore(), new StubHandler(CreateSuccessfulResponse));

        await Assert.ThrowsAsync<InvalidDataException>(() => function.ProcessAsync(message, CancellationToken.None));
    }

    [Fact]
    public async Task ProcessAsync_propagates_compiler_dependency_failures()
    {
        var store = new InMemoryReleaseNoteStore();
        var function = CreateFunction(store, new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() => function.ProcessAsync(SerializeEnvelope(), CancellationToken.None));
        Assert.Empty(store.Documents);
    }

    [Fact]
    public async Task ProcessAsync_propagates_invocation_cancellation()
    {
        var function = CreateFunction(new InMemoryReleaseNoteStore(), new StubHandler(CreateSuccessfulResponse));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            function.ProcessAsync(SerializeEnvelope(), cancellationSource.Token));
    }

    private static CompileReleaseQueueFunction CreateFunction(
        InMemoryReleaseNoteStore store,
        HttpMessageHandler handler,
        ILogger<CompileReleaseQueueFunction>? logger = null)
    {
        var client = new AzureDevOpsClient(new HttpClient(handler), new StaticTokenCredential());
        return new CompileReleaseQueueFunction(new ReleaseNoteCompiler(client, store), logger ?? NullLogger<CompileReleaseQueueFunction>.Instance);
    }

    [Fact]
    public async Task RunAsync_logs_correlated_start_and_success_with_queue_metadata()
    {
        var logger = new CapturingLogger<CompileReleaseQueueFunction>();
        var function = CreateFunction(new InMemoryReleaseNoteStore(), new StubHandler(CreateSuccessfulResponse), logger);
        var context = CreateFunctionContext("message-123", 2);

        await function.RunAsync(SerializeEnvelope(), context.Object);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("Started queued release compilation project-id:42", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information
            && entry.Message.Contains("Persisted queued release project-id:42", StringComparison.Ordinal)
            && entry.Message.Contains("message-123", StringComparison.Ordinal)
            && entry.Message.Contains("dequeue count 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_logs_failure_and_rethrows_with_queue_metadata()
    {
        var logger = new CapturingLogger<CompileReleaseQueueFunction>();
        var function = CreateFunction(
            new InMemoryReleaseNoteStore(),
            new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            logger);
        var context = CreateFunctionContext("message-456", 3);

        await Assert.ThrowsAsync<HttpRequestException>(() => function.RunAsync(SerializeEnvelope(), context.Object));

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error
            && entry.Message.Contains("project-id:42", StringComparison.Ordinal)
            && entry.Message.Contains("message-456", StringComparison.Ordinal)
            && entry.Message.Contains("dequeue count 3", StringComparison.Ordinal)
            && entry.Exception is HttpRequestException);
    }

    private static Mock<FunctionContext> CreateFunctionContext(string messageId, int dequeueCount)
    {
        var bindingContext = new Mock<BindingContext>();
        bindingContext.SetupGet(context => context.BindingData).Returns(new Dictionary<string, object?>
        {
            ["Id"] = messageId,
            ["DequeueCount"] = dequeueCount
        });
        var context = new Mock<FunctionContext>();
        context.SetupGet(value => value.BindingContext).Returns(bindingContext.Object);
        context.SetupGet(value => value.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static string SerializeEnvelope() => JsonSerializer.Serialize(
        new CompilationQueueEnvelope(1, "4a33a6f2-4c6e-451f-82c8-bbf996806034", new CompileRequest
        {
            Organization = "contoso",
            Project = "release-notes",
            ProjectId = "project-id",
            BuildId = 42,
            RepositoryId = "repository-id"
        }),
        SerializerOptions);

    private static HttpResponseMessage CreateSuccessfulResponse(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = path.EndsWith("/changes", StringComparison.Ordinal)
            ? """{"value":[{"id":"commit-1","message":"Implement queue","author":{"displayName":"Ada"},"timestamp":"2026-01-01T12:00:00Z","location":"https://dev.azure.com/commit/commit-1"}]}"""
            : path.EndsWith("/workitems", StringComparison.Ordinal) && path.Contains("/_apis/build/", StringComparison.Ordinal)
                ? """{"value":[{"id":"123"}]}"""
                : path.EndsWith("/_apis/wit/workitems", StringComparison.Ordinal)
                    ? """{"value":[{"id":123,"fields":{"System.Title":"Queue work","System.State":"Done","System.WorkItemType":"Task"},"_links":{"html":{"href":"https://dev.azure.com/workitems/123"}}}]}"""
                    : """{"id":42,"buildNumber":"build-42","sourceBranch":"refs/heads/main","startTime":"2026-01-01T12:00:00Z","uri":"https://dev.azure.com/builds/42"}""";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new AccessToken("test-token", DateTimeOffset.UtcNow.AddMinutes(5));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class InMemoryReleaseNoteStore : IReleaseNoteStore
    {
        private readonly Dictionary<string, ReleaseNoteDocument> documents = [];

        public int UpsertCount { get; private set; }
        public IReadOnlyCollection<ReleaseNoteDocument> Documents => documents.Values;

        public Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpsertCount++;
            documents[document.Id] = document;
            return Task.FromResult(document);
        }

        public Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReleaseNoteDocument>>(documents.Values.ToArray());

        public Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken) =>
            Task.FromResult(documents.TryGetValue(id, out var document) ? document : null);
    }
}