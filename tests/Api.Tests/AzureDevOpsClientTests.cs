using Azure.Core;
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
        var client = new AzureDevOpsClient(new HttpClient(handler), new StaticTokenCredential("token"));

        var changes = await client.GetChangesAsync(new CompileRequest
        {
            Organization = "contoso",
            Project = "release-notes",
            BuildId = 42
        }, CancellationToken.None);

        Assert.Single(changes);
        Assert.Equal("abc123", changes[0].Id);
        Assert.Equal("Ada", changes[0].Author);
        Assert.Equal("Bearer", handler.LastRequest?.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task GetWorkItemsAsync_accepts_string_ids_from_build_work_items()
    {
        var handler = new StubHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.RequestUri!.AbsolutePath.Contains("/build/builds/")
                ? """{"count":1,"value":[{"id":"66","url":"https://dev.azure.com/_apis/wit/workItems/66"}]}"""
                : """{"count":1,"value":[{"id":66,"fields":{"System.Title":"Seed","System.State":"To Do","System.WorkItemType":"Task"},"_links":{"html":{"href":"https://dev.azure.com/wi/66"}}}]}""", Encoding.UTF8, "application/json")
        });
        var client = new AzureDevOpsClient(new HttpClient(handler), new StaticTokenCredential("token"));

        var workItems = await client.GetWorkItemsAsync(new CompileRequest
        {
            Organization = "contoso",
            Project = "release-notes",
            BuildId = 42
        }, CancellationToken.None);

        Assert.Single(workItems);
        Assert.Equal(66, workItems[0].Id);
        Assert.Equal("Seed", workItems[0].Title);
        Assert.Contains("ids=66", handler.LastRequest?.RequestUri?.Query);
    }

    private sealed class StaticTokenCredential(string token) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(token, DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
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