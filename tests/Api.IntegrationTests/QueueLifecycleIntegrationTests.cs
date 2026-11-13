using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Api.Services;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;

namespace Api.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class QueueLifecycleIntegrationTests
{
    private static string StorageConnectionString =>
        Environment.GetEnvironmentVariable("AZURITE_STORAGE_CONNECTION_STRING") ?? "UseDevelopmentStorage=true";
    private const string PoisonMessage = "{";

    [AzureDevOpsQueueLifecycleFact]
    public async Task Functions_host_processes_delayed_work_after_startup()
    {
        var settings = IntegrationSettings.FromEnvironment();
        var suffix = Guid.NewGuid().ToString("N")[..24];
        var queueName = $"release-note-compilation-test-{suffix}";
        var poisonQueueName = $"{queueName}-poison";
        var tableName = $"releases{suffix}";
        var queueOptions = CompilationQueueSubmissionService.CreateClientOptions();
        var workQueue = new QueueClient(StorageConnectionString, queueName, queueOptions);
        var poisonQueue = new QueueClient(StorageConnectionString, poisonQueueName, queueOptions);
        var table = new TableServiceClient(StorageConnectionString).GetTableClient(tableName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        Process? host = null;
        var output = new ConcurrentQueue<string>();

        try
        {
            await workQueue.CreateIfNotExistsAsync(cancellationToken: timeout.Token);
            await table.CreateIfNotExistsAsync(timeout.Token);

            var request = new CompileRequest
            {
                Organization = settings.Organization,
                Project = settings.Project,
                ProjectId = settings.ProjectId,
                RepositoryId = settings.RepositoryId,
                BuildId = settings.BuildId
            };
            await new CompilationQueueSubmissionService(
                workQueue,
                NullLogger<CompilationQueueSubmissionService>.Instance,
                createQueueIfMissing: true).SubmitAsync(request, timeout.Token);

            var (startedHost, hostPort) = StartFunctionsHost(queueName, tableName, output);
            host = startedHost;
            await WaitForHostAsync(host, hostPort, output, timeout.Token);

            var storedRelease = await WaitForReleaseAsync(table, settings.ProjectId, settings.BuildId, timeout.Token);
            Assert.Equal($"{settings.ProjectId}:{settings.BuildId}", storedRelease["id"]);
        }
        finally
        {
            if (host is not null)
            {
                await StopFunctionsHostAsync(host);
            }

            await DeleteTestQueueIfPresentAsync(workQueue);
            await DeleteTestQueueIfPresentAsync(poisonQueue);
        }
    }

    [AzuriteQueueLifecycleFact]
    public async Task Functions_host_retains_poison_after_five_dequeue_attempts()
    {
        var suffix = Guid.NewGuid().ToString("N")[..24];
        var queueName = $"release-note-compilation-test-{suffix}";
        var poisonQueueName = $"{queueName}-poison";
        var tableName = $"releases{suffix}";
        var queueOptions = CompilationQueueSubmissionService.CreateClientOptions();
        var workQueue = new QueueClient(StorageConnectionString, queueName, queueOptions);
        var poisonQueue = new QueueClient(StorageConnectionString, poisonQueueName, queueOptions);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Process? host = null;
        var output = new ConcurrentQueue<string>();

        try
        {
            await workQueue.CreateIfNotExistsAsync(cancellationToken: timeout.Token);
            var poisonReceipt = await workQueue.SendMessageAsync(
                PoisonMessage,
                timeToLive: TimeSpan.FromSeconds(-1),
                cancellationToken: timeout.Token);

            var (startedHost, hostPort) = StartFunctionsHost(queueName, tableName, output);
            host = startedHost;
            await WaitForHostAsync(host, hostPort, output, timeout.Token);

            var poisonMessage = await WaitForPoisonMessageAsync(poisonQueue, timeout.Token);
            Assert.Equal(PoisonMessage, poisonMessage.Body.ToString());

            var poisonMessageId = poisonReceipt.Value.MessageId;
            var failureLines = output
                .Where(line => line.Contains("Failed queued release compilation", StringComparison.Ordinal)
                    && line.Contains(poisonMessageId, StringComparison.Ordinal))
                .ToArray();
            var dequeueCounts = failureLines
                .SelectMany(line => Regex.Matches(line, "dequeue count (\\d+)", RegexOptions.IgnoreCase).Select(match => int.Parse(match.Groups[1].Value)))
                .Distinct()
                .Order()
                .ToArray();
            Assert.Equal([1, 2, 3, 4, 5], dequeueCounts);
        }
        finally
        {
            if (host is not null)
            {
                await StopFunctionsHostAsync(host);
            }

            await DeleteTestQueueIfPresentAsync(workQueue);
            await DeleteTestQueueIfPresentAsync(poisonQueue);
        }
    }

    private static (Process Process, int Port) StartFunctionsHost(string queueName, string tableName, ConcurrentQueue<string> output)
    {
        var configuration = Path.GetFileName(Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.FullName);
        var apiRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Api/bin", configuration, "net10.0"));
        if (!File.Exists(Path.Combine(apiRoot, "functions.metadata")))
        {
            throw new FileNotFoundException("Build the API before running the queue lifecycle test.", Path.Combine(apiRoot, "functions.metadata"));
        }

        var port = GetAvailablePort();
        var funcExecutable = Environment.GetEnvironmentVariable("FUNCTIONS_CORE_TOOLS_EXECUTABLE");
        var startInfo = new ProcessStartInfo(string.IsNullOrWhiteSpace(funcExecutable) ? "func" : funcExecutable)
        {
            WorkingDirectory = apiRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("host");
        startInfo.ArgumentList.Add("start");
        startInfo.ArgumentList.Add("--script-root");
        startInfo.ArgumentList.Add(apiRoot);
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--verbose");
        startInfo.Environment["AzureWebJobsStorage"] = StorageConnectionString;
        startInfo.Environment["CompilationQueueName"] = queueName;
        startInfo.Environment["Storage__ConnectionString"] = StorageConnectionString;
        startInfo.Environment["Storage__TableName"] = tableName;
        startInfo.Environment["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";
        startInfo.Environment["AZURE_FUNCTIONS_ENVIRONMENT"] = "Development";
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["AzureFunctionsJobHost__extensions__queues__maxDequeueCount"] = "5";
        startInfo.Environment["AzureFunctionsJobHost__extensions__queues__visibilityTimeout"] = "00:00:01";
        startInfo.Environment["AzureFunctionsJobHost__extensions__queues__maxPollingInterval"] = "00:00:01";

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.Enqueue(eventArgs.Data);
            }
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.Enqueue(eventArgs.Data);
            }
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return (process, port);
    }

    private static async Task WaitForHostAsync(Process process, int port, ConcurrentQueue<string> output, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}/api/"),
            Timeout = TimeSpan.FromSeconds(2)
        };
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(45))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited)
            {
                throw new InvalidOperationException($"Functions host exited before startup: {string.Join(Environment.NewLine, output)}");
            }

            try
            {
                using var response = await client.GetAsync("releases", cancellationToken);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        throw new TimeoutException($"Functions host did not start: {string.Join(Environment.NewLine, output)}");
    }

    private static async Task<TableEntity> WaitForReleaseAsync(TableClient table, string projectId, int buildId, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await table.GetEntityAsync<TableEntity>(
                    projectId,
                    buildId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    cancellationToken: cancellationToken);
                return response.Value;
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    private static async Task<PeekedMessage> WaitForPoisonMessageAsync(QueueClient poisonQueue, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var response = await poisonQueue.PeekMessagesAsync(maxMessages: 10, cancellationToken: cancellationToken);
                var message = response.Value.FirstOrDefault(value => value.Body.ToString() == PoisonMessage);
                if (message is not null)
                {
                    return message;
                }
            }
            catch (RequestFailedException exception) when (exception.Status == 404)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private static int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task StopFunctionsHostAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
    }

    private static async Task DeleteTestQueueIfPresentAsync(QueueClient queue)
    {
        try
        {
            await queue.DeleteIfExistsAsync();
        }
        catch (RequestFailedException)
        {
        }
    }
}

public sealed class AzuriteQueueLifecycleFactAttribute : FactAttribute
{
    public AzuriteQueueLifecycleFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_AZURITE_INTEGRATION_TESTS") != "1")
        {
            Skip = "Set RUN_AZURITE_INTEGRATION_TESTS=1 when Azurite is available.";
        }
    }
}

public sealed class AzureDevOpsQueueLifecycleFactAttribute : FactAttribute
{
    public AzureDevOpsQueueLifecycleFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_AZURITE_INTEGRATION_TESTS") != "1")
        {
            Skip = "Set RUN_AZURITE_INTEGRATION_TESTS=1 when Azurite is available.";
            return;
        }

        var requiredVariables = new[]
        {
            "AZURE_DEVOPS_ORGANIZATION",
            "AZURE_DEVOPS_PROJECT",
            "AZURE_DEVOPS_PROJECT_ID",
            "AZURE_DEVOPS_REPOSITORY_ID",
            "AZURE_DEVOPS_BUILD_ID"
        };
        var missing = requiredVariables.Where(name => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
        if (missing.Length > 0)
        {
            Skip = $"Configure {string.Join(", ", missing)} and Azure DevOps CLI credentials to run the delayed-worker persistence test.";
        }
    }
}