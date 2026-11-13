using System.Text.Json;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Shared;

namespace Api;

public sealed class CompileReleaseQueueFunction(ReleaseNoteCompiler compiler, ILogger<CompileReleaseQueueFunction> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Function("CompileReleaseQueued")]
    public Task RunAsync(
        [QueueTrigger("%CompilationQueueName%", Connection = "AzureWebJobsStorage")] string message,
        FunctionContext context) =>
        ProcessAsync(
            message,
            context.CancellationToken,
            GetBindingValue(context, "Id"),
            GetDequeueCount(context));

    public Task ProcessAsync(string message, CancellationToken cancellationToken) =>
        ProcessAsync(message, cancellationToken, "unknown", 0);

    private async Task ProcessAsync(string message, CancellationToken cancellationToken, string queueMessageId, int dequeueCount)
    {
        var correlationId = "unknown";
        var releaseId = "unknown";
        try
        {
            CompilationQueueEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<CompilationQueueEnvelope>(message, SerializerOptions);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Compilation queue message must be valid JSON.", exception);
            }

            if (envelope is null || envelope.Version != 1)
            {
                throw new InvalidDataException("Compilation queue message has an unsupported envelope version.");
            }

            correlationId = envelope.CorrelationId;
            if (envelope.Request is not null && envelope.Request.BuildId > 0 && !string.IsNullOrWhiteSpace(envelope.Request.ProjectId))
            {
                releaseId = $"{envelope.Request.ProjectId}:{envelope.Request.BuildId}";
            }

            if (!Guid.TryParse(envelope.CorrelationId, out _)
                || envelope.Request is null
                || string.IsNullOrWhiteSpace(envelope.Request.Organization)
                || string.IsNullOrWhiteSpace(envelope.Request.Project)
                || string.IsNullOrWhiteSpace(envelope.Request.ProjectId)
                || envelope.Request.BuildId <= 0
                || string.IsNullOrWhiteSpace(envelope.Request.RepositoryId))
            {
                throw new InvalidDataException("Compilation queue message is missing valid correlation or build metadata.");
            }

            logger.LogInformation(
                "Started queued release compilation {ReleaseId} with correlation ID {CorrelationId}, queue message ID {QueueMessageId}, and dequeue count {DequeueCount}.",
                releaseId,
                correlationId,
                queueMessageId,
                dequeueCount);

            await compiler.CompileAsync(envelope.Request, cancellationToken);
            logger.LogInformation(
                "Persisted queued release {ReleaseId} with correlation ID {CorrelationId}, queue message ID {QueueMessageId}, and dequeue count {DequeueCount}.",
                releaseId,
                correlationId,
                queueMessageId,
                dequeueCount);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed queued release compilation {ReleaseId} with correlation ID {CorrelationId}, queue message ID {QueueMessageId}, and dequeue count {DequeueCount}.",
                releaseId,
                correlationId,
                queueMessageId,
                dequeueCount);
            throw;
        }
    }

    private static string GetBindingValue(FunctionContext context, string key)
    {
        var binding = context.BindingContext.BindingData.FirstOrDefault(value => string.Equals(value.Key, key, StringComparison.OrdinalIgnoreCase));
        return binding.Value?.ToString() ?? "unknown";
    }

    private static int GetDequeueCount(FunctionContext context) =>
        int.TryParse(GetBindingValue(context, "DequeueCount"), out var dequeueCount) ? dequeueCount : 0;
}