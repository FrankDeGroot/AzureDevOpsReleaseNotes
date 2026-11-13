using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shared;

namespace Api.IntegrationTests;

public sealed class ReleasePollingClient(
    HttpClient client,
    TimeProvider? timeProvider = null,
    Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Func<TimeSpan, CancellationToken, Task> delay = delayAsync ?? ((duration, cancellationToken) => Task.Delay(duration, cancellationToken));

    public async Task EnsureAbsentAsync(Uri releaseUrl, string releaseId, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(releaseUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Release {releaseId} already exists before compilation. Prepare a fresh Azure DevOps build with scripts/Initialize-AzureDevOpsTestProject.ps1 -NewBuild.");
        }

        throw new HttpRequestException(
            $"Release preflight for {releaseId} returned HTTP {(int)response.StatusCode}: {body}",
            null,
            response.StatusCode);
    }

    public async Task<ReleaseNoteDocument> PollAsync(
        Uri releaseUrl,
        string releaseId,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default)
    {
        var started = clock.GetTimestamp();
        var lastResponse = "No release response received.";

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = timeout - clock.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                throw CreateTimeoutException(releaseId, lastResponse);
            }

            HttpStatusCode responseStatus;
            string responseBody;
            try
            {
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                requestTimeout.CancelAfter(remaining);
                using var response = await client.GetAsync(releaseUrl, requestTimeout.Token);
                responseStatus = response.StatusCode;
                responseBody = await response.Content.ReadAsStringAsync(requestTimeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw CreateTimeoutException(releaseId, lastResponse);
            }

            lastResponse = $"HTTP {(int)responseStatus}: {responseBody}";

            if (responseStatus == HttpStatusCode.OK)
            {
                ReleaseNoteDocument? document;
                try
                {
                    document = JsonSerializer.Deserialize<ReleaseNoteDocument>(responseBody, CompiledReleaseFixture.JsonOptions);
                }
                catch (JsonException exception)
                {
                    throw new InvalidDataException($"Release {releaseId} returned invalid JSON: {responseBody}", exception);
                }

                if (document is null || !string.Equals(document.Id, releaseId, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"Release GET returned a missing or mismatched document for {releaseId}: {responseBody}");
                }

                return document;
            }

            if (responseStatus != HttpStatusCode.NotFound)
            {
                throw new HttpRequestException($"Release polling for {releaseId} failed: {lastResponse}", null, responseStatus);
            }

            remaining = timeout - clock.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
            {
                throw CreateTimeoutException(releaseId, lastResponse);
            }

            await delay(pollInterval < remaining ? pollInterval : remaining, cancellationToken);
        }
    }

    private static TimeoutException CreateTimeoutException(string releaseId, string lastResponse) =>
        new($"Timed out waiting for release {releaseId}. Last response: {lastResponse} Inspect processing logs and the poison queue.");
}