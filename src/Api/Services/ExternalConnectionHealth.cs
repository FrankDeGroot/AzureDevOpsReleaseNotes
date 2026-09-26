using System.Diagnostics;

namespace Api.Services;

public interface IExternalConnectionHealthCheck
{
    string Name { get; }
    Task<string> CheckAsync(CancellationToken cancellationToken);
}

public sealed class AzureDevOpsHealthCheck(HttpClient httpClient) : IExternalConnectionHealthCheck
{
    public string Name => "azureDevOps";

    public async Task<string> CheckAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("https://dev.azure.com/", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((int)response.StatusCode >= 500)
        {
            response.EnsureSuccessStatusCode();
        }

        return $"Reachable (HTTP {(int)response.StatusCode}).";
    }
}

public sealed class ExternalConnectionHealthService(
    IEnumerable<IExternalConnectionHealthCheck> checks,
    ILogger<ExternalConnectionHealthService> logger)
{
    public async Task<HealthResponse> CheckAsync(CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(checks.Select(check => RunCheckAsync(check, cancellationToken)));
        return new HealthResponse(
            results.All(result => result.Status == "healthy") ? "healthy" : "unhealthy",
            DateTimeOffset.UtcNow,
            results);
    }

    private async Task<ExternalConnectionHealthResponse> RunCheckAsync(
        IExternalConnectionHealthCheck check,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            var description = await check.CheckAsync(cancellationToken);
            return new ExternalConnectionHealthResponse(check.Name, "healthy", description, ElapsedMilliseconds(startedAt));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "External connection health check failed for {ConnectionName}.", check.Name);
            return new ExternalConnectionHealthResponse(check.Name, "unhealthy", "Connection check failed.", ElapsedMilliseconds(startedAt));
        }
    }

    private static long ElapsedMilliseconds(long startedAt) =>
        (long)Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
}

/// <summary>Represents the application and external connection health.</summary>
public sealed record HealthResponse(
    string Status,
    DateTimeOffset CheckedAt,
    IReadOnlyList<ExternalConnectionHealthResponse> Connections);

/// <summary>Represents the health of one external connection.</summary>
public sealed record ExternalConnectionHealthResponse(
    string Name,
    string Status,
    string Description,
    long DurationMilliseconds);