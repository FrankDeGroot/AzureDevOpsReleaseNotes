using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests;

[Trait("Category", "Integration")]
[Trait("Target", "Azure")]
public sealed class HostedAuthorizationTests
{
    [Theory]
    [InlineData("anonymous")]
    [InlineData("legacy-key")]
    [InlineData("malformed-token")]
    [InlineData("spoofed-header")]
    [InlineData("wrong-audience")]
    [InlineData("expired-unsigned")]
    public async Task Compile_rejects_untrusted_callers_without_redirect(string credentialKind)
    {
        var settings = IntegrationSettings.FromEnvironment();
        using var client = settings.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        var relativeUrl = "releases/compile";
        switch (credentialKind)
        {
            case "legacy-key":
                client.DefaultRequestHeaders.Add("x-functions-key", "obsolete-test-key");
                relativeUrl += "?code=obsolete-test-key";
                break;
            case "malformed-token":
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
                break;
            case "spoofed-header":
                var principal = new
                {
                    auth_typ = "aad", claims = new[]
                    {
                        new { typ = "tid", val = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty },
                        new { typ = "oid", val = "11111111-1111-1111-1111-111111111111" }
                    }
                };
                client.DefaultRequestHeaders.Add("X-MS-CLIENT-PRINCIPAL", Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(principal)));
                break;
            case "wrong-audience":
                var wrongAudienceToken = Environment.GetEnvironmentVariable("RELEASE_NOTES_API_WRONG_AUDIENCE_TOKEN");
                if (string.IsNullOrWhiteSpace(wrongAudienceToken))
                {
                    throw new InvalidOperationException("Azure authorization checks require a short-lived RELEASE_NOTES_API_WRONG_AUDIENCE_TOKEN.");
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongAudienceToken);
                break;
            case "expired-unsigned":
                var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { alg = "none", typ = "JWT" }));
                var payload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new
                {
                    aud = Environment.GetEnvironmentVariable("RELEASE_NOTES_AUTH_AUDIENCE"), exp = 1, nbf = 0
                }));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{header}.{payload}.");
                break;
        }

        var response = await client.PostAsJsonAsync(relativeUrl, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Public_routes_remain_accessible_without_credentials()
    {
        var settings = IntegrationSettings.FromEnvironment();
        using var client = settings.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri(settings.ApiBaseUrl, "../"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("releases")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("releases/nonexistent-auth-test-record" )).StatusCode);
        var health = await client.GetAsync("health");
        Assert.True(health.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);
    }
}