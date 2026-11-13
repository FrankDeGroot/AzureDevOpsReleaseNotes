using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared;

namespace Api.Services;

public sealed record CompilationQueueEnvelope(int Version, string CorrelationId, CompileRequest Request);

public interface ICompilationQueueSubmission
{
    Task SubmitAsync(CompileRequest request, CancellationToken cancellationToken);
}

public sealed class CompilationQueueMessageTooLargeException(int encodedSizeBytes, int maximumSizeBytes)
    : Exception($"The encoded compilation message is {encodedSizeBytes} bytes; the maximum is {maximumSizeBytes} bytes.")
{
    public int EncodedSizeBytes { get; } = encodedSizeBytes;
    public int MaximumSizeBytes { get; } = maximumSizeBytes;
}

public sealed class CompilationQueueSubmissionService(
    QueueClient queueClient,
    ILogger<CompilationQueueSubmissionService> logger,
    bool createQueueIfMissing = false) : ICompilationQueueSubmission
{
    public const string QueueName = "release-note-compilation";
    public const int MaximumEncodedMessageSizeBytes = 64 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static QueueClientOptions CreateClientOptions() => new(QueueClientOptions.ServiceVersion.V2026_06_06)
    {
        MessageEncoding = QueueMessageEncoding.Base64
    };

    public static QueueClient CreateQueueClient(IConfiguration configuration, TokenCredential credential)
    {
        var options = CreateClientOptions();
        var queueName = string.IsNullOrWhiteSpace(configuration["CompilationQueueName"])
            ? QueueName
            : configuration["CompilationQueueName"]!;
        var connectionString = configuration["AzureWebJobsStorage"];
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return new QueueClient(connectionString, queueName, options);
        }

        var queueServiceUri = configuration["AzureWebJobsStorage:queueServiceUri"];
        if (!Uri.TryCreate(queueServiceUri, UriKind.Absolute, out var serviceUri)
            || (serviceUri.Scheme != Uri.UriSchemeHttps && serviceUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException(
                "Configure AzureWebJobsStorage as a connection string or set AzureWebJobsStorage:queueServiceUri to the queue service endpoint.");
        }

        return new QueueClient(new Uri(serviceUri, queueName), credential, options);
    }

    public async Task SubmitAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        var envelope = new CompilationQueueEnvelope(1, Guid.NewGuid().ToString("D"), request);
        var message = JsonSerializer.Serialize(envelope, SerializerOptions);
        var messageByteCount = Encoding.UTF8.GetByteCount(message);
        var encodedSizeBytes = checked(((messageByteCount + 2) / 3) * 4);
        if (encodedSizeBytes > MaximumEncodedMessageSizeBytes)
        {
            throw new CompilationQueueMessageTooLargeException(encodedSizeBytes, MaximumEncodedMessageSizeBytes);
        }

        var releaseId = $"{request.ProjectId}:{request.BuildId}";
        logger.LogInformation("Submitting release compilation {ReleaseId} with correlation ID {CorrelationId}.", releaseId, envelope.CorrelationId);
        try
        {
            if (createQueueIfMissing)
            {
                await queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }

            var response = await queueClient.SendMessageAsync(
                message,
                timeToLive: TimeSpan.FromSeconds(-1),
                cancellationToken: cancellationToken);
            logger.LogInformation(
                "Queued release compilation {ReleaseId} with correlation ID {CorrelationId} and queue message ID {QueueMessageId}.",
                releaseId,
                envelope.CorrelationId,
                response?.Value?.MessageId ?? "unknown");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to submit release compilation {ReleaseId} with correlation ID {CorrelationId}.", releaseId, envelope.CorrelationId);
            throw;
        }
    }
}