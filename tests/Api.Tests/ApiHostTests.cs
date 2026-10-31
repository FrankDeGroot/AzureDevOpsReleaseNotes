using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Azure.Core;
using Api.Authentication;
using Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shared;

namespace Api.Tests;

public sealed class ApiHostTests
{
    [Theory]
    [InlineData("")]
    [InlineData("InstrumentationKey=00000000-0000-0000-0000-000000000001;IngestionEndpoint=http://localhost:1/")]
    public async Task Optional_telemetry_preserves_endpoint_contract(string connectionString)
    {
        using var host = new TestHost(settings: new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = connectionString
        });
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/releases")).StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/releases/project%3A42")]
    [InlineData("/_content/BlazorApplicationInsights/BlazorApplicationInsights.lib.module.js")]
    public async Task Client_routes_and_framework_assets_are_served(string path)
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Client_boot_script_from_served_html_is_available()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var html = await client.GetStringAsync("/");
        var script = System.Text.RegularExpressions.Regex.Match(html, "src=\"(_framework/blazor[^\"]+)\"");
        Assert.True(script.Success);
        var response = await client.GetAsync("/" + script.Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task Compile_rejects_invalid_input_as_json(string body)
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var response = await client.PostAsync("/api/releases/compile", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, host.Store.Writes);
    }

    [Fact]
    public async Task Compile_preserves_response_and_defaults_project_id()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/releases/compile", new CompileRequest
        {
            Organization = "test", Project = "project", RepositoryId = "repo", BuildId = 42
        });
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<ReleaseNoteDocument>();
        Assert.Equal("project:42", document!.Id);
        Assert.Equal("project", document.ProjectId);
        Assert.Equal(1, host.Store.Writes);
        Assert.Contains("\"buildNumber\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_and_detail_preserve_filters_and_not_found_errors()
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var list = await client.GetAsync("/api/releases?project=one%20two");
        list.EnsureSuccessStatusCode();
        Assert.Equal("one two", host.Store.Project);
        var missing = await client.GetAsync("/api/releases/missing?projectId=project-id");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("project-id", host.Store.ProjectId);
        Assert.Contains("Release note was not found.", await missing.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.OK)]
    [InlineData(true, HttpStatusCode.ServiceUnavailable)]
    public async Task Health_preserves_dependency_status(bool fail, HttpStatusCode expected)
    {
        using var host = new TestHost(failHealth: fail);
        using var client = host.CreateClient();
        var response = await client.GetAsync("/api/health");
        Assert.Equal(expected, response.StatusCode);
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal(fail ? "unhealthy" : "healthy", health!.Status);
        Assert.True(host.Health.HasBoundedCancellation);
    }

    [Theory]
    [InlineData("/api/missing")]
    [InlineData("/missing.js")]
    [InlineData("/_framework/missing.wasm")]
    [InlineData("/missing.json")]
    public async Task Unknown_api_and_assets_do_not_fall_back_to_html(string path)
    {
        using var host = new TestHost();
        using var client = host.CreateClient();
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<html", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("Production", false)]
    [InlineData("Development", true)]
    public async Task Anonymous_compile_is_closed_outside_local_development(string environment, bool azure)
    {
        using var host = new TestHost(environment, azure);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("x-functions-key", "legacy");
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", "not-a-platform-identity");
        var response = await client.PostAsJsonAsync("/api/releases/compile?code=legacy", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(0, host.Store.Writes);
    }

    [Theory]
    [InlineData(false, true, true, HttpStatusCode.Unauthorized)]
    [InlineData(true, false, true, HttpStatusCode.Unauthorized)]
    [InlineData(true, true, false, HttpStatusCode.Unauthorized)]
    [InlineData(true, true, true, HttpStatusCode.BadRequest)]
    public async Task Platform_identity_requires_host_boundary_configuration_and_valid_claims(bool azure, bool enabled, bool validTenant, HttpStatusCode expected)
    {
        using var host = new TestHost("Production", azure, settings: new Dictionary<string, string?>
        {
            ["WEBSITE_AUTH_ENABLED"] = enabled.ToString(),
            ["EasyAuth:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["EasyAuth:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["EasyAuth:Audience"] = "api://22222222-2222-2222-2222-222222222222"
        });
        using var client = host.CreateClient();
        var tenant = validTenant ? "11111111-1111-1111-1111-111111111111" : "33333333-3333-3333-3333-333333333333";
        var principal = System.Text.Json.JsonSerializer.Serialize(new
        {
            auth_typ = "aad", claims = new[]
            {
                new { typ = "tid", val = tenant },
                new { typ = "oid", val = "44444444-4444-4444-4444-444444444444" }
            }
        });
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Convert.ToBase64String(Encoding.UTF8.GetBytes(principal)));
        var response = await client.PostAsJsonAsync("/api/releases/compile", new { });
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(0, host.Store.Writes);
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("e30=")]
    [InlineData("bnVsbA==")]
    public async Task Malformed_platform_principals_are_rejected(string header)
    {
        using var host = new TestHost("Production", true, settings: new Dictionary<string, string?>
        {
            ["WEBSITE_AUTH_ENABLED"] = "true",
            ["EasyAuth:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["EasyAuth:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["EasyAuth:Audience"] = "api://22222222-2222-2222-2222-222222222222"
        });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", header);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/releases/compile", new { })).StatusCode);
        Assert.Equal(0, host.Store.Writes);
    }

    [Fact]
    public async Task Authenticated_compile_uses_test_only_identity_and_public_reads_remain_open()
    {
        using var host = new TestHost("Production", authenticated: true);
        using var client = host.CreateClient();
        var response = await client.PostAsJsonAsync("/api/releases/compile", new CompileRequest
        {
            Organization = "test", Project = "project", RepositoryId = "repo", BuildId = 42
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var publicHost = new TestHost("Production");
        using var publicClient = publicHost.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/api/releases")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await publicClient.GetAsync("/api/releases/missing")).StatusCode);
    }

    private sealed class TestHost(string environment = "Development", bool azure = false, bool authenticated = false, bool failHealth = false, Dictionary<string, string?>? settings = null)
        : WebApplicationFactory<Program>
    {
        public MemoryStore Store { get; } = new();
        public HealthCheck Health { get; } = new(failHealth);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WEBSITE_SITE_NAME"] = azure ? "test-app" : "",
                ["WEBSITE_INSTANCE_ID"] = "",
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "",
                ["Logging:LogLevel:Default"] = "Error"
            }).AddInMemoryCollection(settings ?? []));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReleaseNoteStore>();
                services.RemoveAll<IExternalConnectionHealthCheck>();
                services.AddSingleton<IReleaseNoteStore>(Store);
                services.AddSingleton<IExternalConnectionHealthCheck>(Health);
                services.AddSingleton<TokenCredential>(new TestCredential());
                services.AddHttpClient<AzureDevOpsClient>().ConfigurePrimaryHttpMessageHandler(() => new DevOpsHandler());
                if (authenticated)
                {
                    services.AddAuthentication(options => options.DefaultScheme = "Test")
                        .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
                }
            });
        }
    }

    private sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, Microsoft.Extensions.Logging.ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "test")], "Test")), "Test")));
    }

    private sealed class MemoryStore : IReleaseNoteStore
    {
        public int Writes { get; private set; }
        public string? Project { get; private set; }
        public string? ProjectId { get; private set; }
        public Task<ReleaseNoteDocument> UpsertAsync(ReleaseNoteDocument document, CancellationToken cancellationToken)
        {
            Writes++;
            return Task.FromResult(document);
        }
        public Task<IReadOnlyList<ReleaseNoteDocument>> ListAsync(string? project, CancellationToken cancellationToken)
        {
            Project = project;
            return Task.FromResult<IReadOnlyList<ReleaseNoteDocument>>([]);
        }
        public Task<ReleaseNoteDocument?> GetAsync(string id, string? projectId, CancellationToken cancellationToken)
        {
            ProjectId = projectId;
            return Task.FromResult<ReleaseNoteDocument?>(null);
        }
    }

    private sealed class HealthCheck(bool fail) : IExternalConnectionHealthCheck
    {
        public string Name => "test";
        public bool HasBoundedCancellation { get; private set; }
        public Task<string> CheckAsync(CancellationToken cancellationToken)
        {
            HasBoundedCancellation = cancellationToken.CanBeCanceled;
            return fail ? Task.FromException<string>(new InvalidOperationException("unavailable")) : Task.FromResult("Reachable.");
        }
    }

    private sealed class TestCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("test", DateTimeOffset.UtcNow.AddMinutes(5));
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class DevOpsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(request.RequestUri!.AbsolutePath.EndsWith("/42")
                    ? """{"id":42,"buildNumber":"42","sourceBranch":"refs/heads/main","finishTime":"2026-01-01T00:00:00Z","_links":{"web":{"href":"https://example.test/42"}}}"""
                    : """{"value":[]}""", Encoding.UTF8, "application/json")
            });
    }
}