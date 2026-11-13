using System.Text;
using System.Text.Json;
using Api.Services;
using Azure;
using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared;

namespace Api.Tests;

public sealed class CompilationQueueSubmissionTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SubmitAsync_sends_versioned_credential_free_envelope_with_non_expiring_ttl_and_cancellation()
    {
        var client = CreateQueueClient();
        string? submittedMessage = null;
        TimeSpan? submittedTimeToLive = null;
        using var cancellationSource = new CancellationTokenSource();
        client.Setup(queue => queue.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, TimeSpan?, TimeSpan?, CancellationToken>((message, _, timeToLive, token) =>
            {
                submittedMessage = message;
                submittedTimeToLive = timeToLive;
                Assert.Equal(cancellationSource.Token, token);
            })
            .ReturnsAsync((Response<SendReceipt>)null!);

        var request = CreateRequest("release-project");
        await new CompilationQueueSubmissionService(client.Object, NullLogger<CompilationQueueSubmissionService>.Instance).SubmitAsync(request, cancellationSource.Token);

        Assert.NotNull(submittedMessage);
        var envelope = JsonSerializer.Deserialize<CompilationQueueEnvelope>(submittedMessage!, SerializerOptions);
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope.Version);
        Assert.True(Guid.TryParse(envelope.CorrelationId, out _));
        Assert.Equal(request.Organization, envelope.Request.Organization);
        Assert.Equal(request.Project, envelope.Request.Project);
        Assert.Equal(request.ProjectId, envelope.Request.ProjectId);
        Assert.Equal(request.BuildId, envelope.Request.BuildId);
        Assert.Equal(request.RepositoryId, envelope.Request.RepositoryId);
        Assert.DoesNotContain("token", submittedMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromSeconds(-1), submittedTimeToLive);
        client.Verify(queue => queue.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), cancellationSource.Token), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_accepts_encoded_message_at_limit_and_rejects_one_byte_over()
    {
        var targetMessageBytes = CompilationQueueSubmissionService.MaximumEncodedMessageSizeBytes / 4 * 3;
        var baselineRequest = CreateRequest(string.Empty);
        var baselineEnvelope = new CompilationQueueEnvelope(1, Guid.Empty.ToString("D"), baselineRequest);
        var baselineMessage = JsonSerializer.Serialize(baselineEnvelope, SerializerOptions);
        var organizationLength = targetMessageBytes - Encoding.UTF8.GetByteCount(baselineMessage);
        var boundaryRequest = CreateRequest(new string('x', organizationLength));
        Assert.Equal(CompilationQueueSubmissionService.MaximumEncodedMessageSizeBytes, GetEncodedSize(boundaryRequest));

        var client = CreateQueueClient();
        client.Setup(queue => queue.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Response<SendReceipt>)null!);
        var service = new CompilationQueueSubmissionService(client.Object, NullLogger<CompilationQueueSubmissionService>.Instance);

        await service.SubmitAsync(boundaryRequest, CancellationToken.None);
        var error = await Assert.ThrowsAsync<CompilationQueueMessageTooLargeException>(
            () => service.SubmitAsync(CreateRequest(new string('x', organizationLength + 1)), CancellationToken.None));

        Assert.Equal(CompilationQueueSubmissionService.MaximumEncodedMessageSizeBytes + 4, error.EncodedSizeBytes);
        client.Verify(queue => queue.SendMessageAsync(
            It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_propagates_storage_send_failures()
    {
        var client = CreateQueueClient();
        client.Setup(queue => queue.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(503, "Queue unavailable."));

        await Assert.ThrowsAsync<RequestFailedException>(() =>
            new CompilationQueueSubmissionService(client.Object, NullLogger<CompilationQueueSubmissionService>.Instance).SubmitAsync(CreateRequest("release-project"), CancellationToken.None));
    }

    [Fact]
    public void CreateClientOptions_enables_base64_message_encoding()
    {
        Assert.Equal(QueueMessageEncoding.Base64, CompilationQueueSubmissionService.CreateClientOptions().MessageEncoding);
    }

    [Fact]
    public void CreateQueueClient_uses_the_local_storage_connection_string()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true"
            })
            .Build();

        var client = CompilationQueueSubmissionService.CreateQueueClient(configuration, Mock.Of<TokenCredential>());

        Assert.Equal("127.0.0.1", client.Uri.Host);
        Assert.EndsWith(CompilationQueueSubmissionService.QueueName, client.Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateQueueClient_allows_a_test_queue_name_override()
    {
        const string testQueueName = "release-note-compilation-test-abc123";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
                ["CompilationQueueName"] = testQueueName
            })
            .Build();

        var client = CompilationQueueSubmissionService.CreateQueueClient(configuration, Mock.Of<TokenCredential>());

        Assert.EndsWith(testQueueName, client.Uri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateQueueClient_uses_identity_with_the_configured_queue_service_uri()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureWebJobsStorage:queueServiceUri"] = "https://releaseaccount.queue.core.windows.net"
            })
            .Build();

        var client = CompilationQueueSubmissionService.CreateQueueClient(configuration, Mock.Of<TokenCredential>());

        Assert.Equal("https://releaseaccount.queue.core.windows.net/release-note-compilation", client.Uri.AbsoluteUri);
    }

    [Fact]
    public void CreateQueueClient_fails_with_actionable_error_when_storage_is_not_configured()
    {
        var configuration = new ConfigurationBuilder().Build();

        var error = Assert.Throws<InvalidOperationException>(
            () => CompilationQueueSubmissionService.CreateQueueClient(configuration, Mock.Of<TokenCredential>()));

        Assert.Contains("AzureWebJobsStorage", error.Message, StringComparison.Ordinal);
        Assert.Contains("queueServiceUri", error.Message, StringComparison.Ordinal);
    }

    private static Mock<QueueClient> CreateQueueClient() => new(MockBehavior.Strict);

    private static int GetEncodedSize(CompileRequest request)
    {
        var envelope = new CompilationQueueEnvelope(1, Guid.Empty.ToString("D"), request);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope, SerializerOptions))).Length;
    }

    private static CompileRequest CreateRequest(string organization) => new()
    {
        Organization = organization,
        Project = "Release Project",
        ProjectId = "project-id",
        BuildId = 42,
        RepositoryId = "repository-id"
    };
}