using System.Net;
using System.Text;
using System.Text.Json;
using Api.Services;
using Azure;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shared;

namespace Api.Tests;

public sealed class ReleaseFunctionsTests
{
    [Fact]
    public async Task Compile_returns_acceptance_acknowledgment_and_escaped_release_location()
    {
        var requestBody = """{"organization":"contoso","project":"release-notes","projectId":"project/id&x","buildId":42,"repositoryId":"repo-id"}""";
        var (request, response) = CreateRequest(requestBody);
        var queueSubmission = new Mock<ICompilationQueueSubmission>(MockBehavior.Strict);
        CompileRequest? submittedRequest = null;
        queueSubmission.Setup(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CompileRequest, CancellationToken>((value, _) => submittedRequest = value)
            .Returns(Task.CompletedTask);
        var store = new Mock<IReleaseNoteStore>(MockBehavior.Strict);

        var result = await new ReleaseFunctions(queueSubmission.Object, store.Object, NullLogger<ReleaseFunctions>.Instance).Compile(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Accepted, result.StatusCode);
        Assert.NotNull(submittedRequest);
        Assert.Equal("project/id&x", submittedRequest.ProjectId);
        Assert.Equal("project/id&x:42", submittedRequest.ProjectId + ":" + submittedRequest.BuildId);
        var expectedUrl = "/api/releases/project%2Fid%26x%3A42?projectId=project%2Fid%26x";
        Assert.Equal(expectedUrl, result.Headers.GetValues("Location").Single());

        result.Body.Position = 0;
        var acknowledgment = await JsonSerializer.DeserializeAsync<CompileReleaseAcknowledgment>(result.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(acknowledgment);
        Assert.Equal("project/id&x:42", acknowledgment.Id);
        Assert.Equal("project/id&x", acknowledgment.ProjectId);
        Assert.Equal(expectedUrl, acknowledgment.ReleaseUrl);
        queueSubmission.Verify(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compile_uses_project_name_when_optional_project_id_is_missing()
    {
        var (request, _) = CreateRequest("""{"organization":"contoso","project":"release-notes","buildId":42,"repositoryId":"repo-id"}""");
        var queueSubmission = new Mock<ICompilationQueueSubmission>();
        CompileRequest? submittedRequest = null;
        queueSubmission.Setup(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CompileRequest, CancellationToken>((value, _) => submittedRequest = value)
            .Returns(Task.CompletedTask);

        await new ReleaseFunctions(queueSubmission.Object, Mock.Of<IReleaseNoteStore>(), NullLogger<ReleaseFunctions>.Instance).Compile(request, CancellationToken.None);

        Assert.NotNull(submittedRequest);
        Assert.Equal("release-notes", submittedRequest.ProjectId);
    }

    private static (HttpRequestData Request, HttpResponseData Response) CreateRequest(string body)
    {
        var context = new Mock<FunctionContext>();
        var services = new ServiceCollection();
        services.AddSingleton<IOptions<WorkerOptions>>(Options.Create(new WorkerOptions
        {
            Serializer = new JsonObjectSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        }));
        context.SetupGet(value => value.InstanceServices).Returns(services.BuildServiceProvider());
        var request = new Mock<HttpRequestData>(context.Object);
        request.SetupGet(value => value.Body).Returns(new MemoryStream(Encoding.UTF8.GetBytes(body)));
        request.SetupGet(value => value.Url).Returns(new Uri("https://example.test/api/releases/compile"));

        var response = new Mock<HttpResponseData>(context.Object);
        response.SetupProperty(value => value.StatusCode);
        response.SetupProperty(value => value.Headers, new HttpHeadersCollection());
        response.SetupProperty(value => value.Body, new MemoryStream());
        request.Setup(value => value.CreateResponse()).Returns(response.Object);

        return (request.Object, response.Object);
    }

    [Fact]
    public async Task Compile_returns_bad_request_for_malformed_json_without_submitting()
    {
        var (request, response) = CreateRequest("{");
        var queueSubmission = new Mock<ICompilationQueueSubmission>(MockBehavior.Strict);
        var store = new Mock<IReleaseNoteStore>(MockBehavior.Strict);

        var result = await new ReleaseFunctions(queueSubmission.Object, store.Object, NullLogger<ReleaseFunctions>.Instance)
            .Compile(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var error = await ReadErrorAsync(result);
        Assert.Equal("Request body must be valid JSON.", error);
        queueSubmission.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compile_returns_bad_request_for_missing_metadata_without_submitting()
    {
        var (request, _) = CreateRequest("""{"organization":"contoso","project":"release-notes","buildId":42}""");
        var queueSubmission = new Mock<ICompilationQueueSubmission>(MockBehavior.Strict);
        var store = new Mock<IReleaseNoteStore>(MockBehavior.Strict);

        var result = await new ReleaseFunctions(queueSubmission.Object, store.Object, NullLogger<ReleaseFunctions>.Instance)
            .Compile(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        queueSubmission.VerifyNoOtherCalls();
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compile_returns_descriptive_bad_request_for_oversized_queue_message()
    {
        var (request, _) = CreateRequest("""{"organization":"contoso","project":"release-notes","buildId":42,"repositoryId":"repo-id"}""");
        var queueSubmission = new Mock<ICompilationQueueSubmission>(MockBehavior.Strict);
        queueSubmission.Setup(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CompilationQueueMessageTooLargeException(65540, 65536));
        var store = new Mock<IReleaseNoteStore>(MockBehavior.Strict);

        var result = await new ReleaseFunctions(queueSubmission.Object, store.Object, NullLogger<ReleaseFunctions>.Instance)
            .Compile(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var error = await ReadErrorAsync(result);
        Assert.Contains("encoded compilation message", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maximum is 65536 bytes", error, StringComparison.Ordinal);
        queueSubmission.Verify(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Compile_returns_sanitized_service_unavailable_and_logs_queue_failure()
    {
        var (request, _) = CreateRequest("""{"organization":"contoso","project":"release-notes","buildId":42,"repositoryId":"repo-id"}""");
        var queueSubmission = new Mock<ICompilationQueueSubmission>(MockBehavior.Strict);
        queueSubmission.Setup(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "secret storage detail"));
        var store = new Mock<IReleaseNoteStore>(MockBehavior.Strict);
        var logger = new CapturingLogger<ReleaseFunctions>();

        var result = await new ReleaseFunctions(queueSubmission.Object, store.Object, logger)
            .Compile(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        var error = await ReadErrorAsync(result);
        Assert.Equal("The compilation request could not be queued. Please retry.", error);
        Assert.DoesNotContain("secret", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception is RequestFailedException);
        queueSubmission.Verify(queue => queue.SubmitAsync(It.IsAny<CompileRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        store.VerifyNoOtherCalls();
    }

    private static async Task<string> ReadErrorAsync(HttpResponseData response)
    {
        response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(response.Body);
        return document.RootElement.GetProperty("error").GetString()!;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, exception));
        }
    }
}