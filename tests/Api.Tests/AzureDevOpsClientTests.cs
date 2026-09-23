using System.Net;
using System.Text;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Shared;

namespace Api.Tests;

public sealed class AzureDevOpsClientTests
{
    [Fact]
    public async Task GetChangesAsync_maps_mock_azure_devops_response()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"count":1,"value":[{"id":"abc123","message":"Ship it","author":{"displayName":"Ada"},"timestamp":"2026-01-01T12:00:00Z","location":"https://dev.azure.com/commit/abc123"}]}""", Encoding.UTF8, "application/json")
        });
        var client = new AzureDevOpsClient(new HttpClient(handler), new ConfigurationBuilder().Build());

        var changes = await client.GetChangesAsync(new CompileRequest
        {
            Organization = "contoso",
            Project = "release-notes",
            BuildId = 42,
            AccessToken = "token"
        }, CancellationToken.None);

        Assert.Single(changes);
        Assert.Equal("abc123", changes[0].Id);
        Assert.Equal("Ada", changes[0].Author);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responseFactory(request));
        }
    }
}