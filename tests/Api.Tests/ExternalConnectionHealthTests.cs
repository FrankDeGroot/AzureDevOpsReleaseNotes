using System.Net;
using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class ExternalConnectionHealthTests
{
    [Fact]
    public async Task CheckAsync_reports_all_healthy_connections()
    {
        var checks = new IExternalConnectionHealthCheck[]
        {
            new StubHealthCheck("azureDevOps", "Reachable."),
            new StubHealthCheck("cosmosDb", "Database and container reachable.")
        };
        var service = new ExternalConnectionHealthService(checks, NullLogger<ExternalConnectionHealthService>.Instance);

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.Equal("healthy", result.Status);
        Assert.Equal(2, result.Connections.Count);
        Assert.All(result.Connections, connection => Assert.Equal("healthy", connection.Status));
    }

    [Fact]
    public async Task CheckAsync_reports_sanitized_connection_failure()
    {
        var service = new ExternalConnectionHealthService(
            [new StubHealthCheck("cosmosDb", new InvalidOperationException("secret connection detail"))],
            NullLogger<ExternalConnectionHealthService>.Instance);

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.Equal("unhealthy", result.Status);
        var connection = Assert.Single(result.Connections);
        Assert.Equal("unhealthy", connection.Status);
        Assert.Equal("Connection check failed.", connection.Description);
        Assert.DoesNotContain("secret", connection.Description);
    }

    [Fact]
    public async Task AzureDevOpsHealthCheck_treats_client_error_as_reachable()
    {
        var healthCheck = new AzureDevOpsHealthCheck(new HttpClient(new StubHandler(HttpStatusCode.Unauthorized)));

        var description = await healthCheck.CheckAsync(CancellationToken.None);

        Assert.Equal("Reachable (HTTP 401).", description);
    }

    [Fact]
    public async Task AzureDevOpsHealthCheck_rejects_server_error()
    {
        var healthCheck = new AzureDevOpsHealthCheck(new HttpClient(new StubHandler(HttpStatusCode.ServiceUnavailable)));

        await Assert.ThrowsAsync<HttpRequestException>(() => healthCheck.CheckAsync(CancellationToken.None));
    }

    private sealed class StubHealthCheck : IExternalConnectionHealthCheck
    {
        private readonly string? description;
        private readonly Exception? exception;

        public StubHealthCheck(string name, string description)
        {
            Name = name;
            this.description = description;
        }

        public StubHealthCheck(string name, Exception exception)
        {
            Name = name;
            this.exception = exception;
        }

        public string Name { get; }

        public Task<string> CheckAsync(CancellationToken cancellationToken) =>
            exception is null ? Task.FromResult(description!) : Task.FromException<string>(exception);
    }

    private sealed class StubHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}