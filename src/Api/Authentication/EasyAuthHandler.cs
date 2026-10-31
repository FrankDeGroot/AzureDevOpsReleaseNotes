using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Api.Authentication;

public sealed class EasyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "EasyAuth";

    public static bool IsAppService(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["WEBSITE_SITE_NAME"]) ||
        !string.IsNullOrWhiteSpace(configuration["WEBSITE_INSTANCE_ID"]);

    public static bool IsLocalDevelopment(IHostEnvironment environment, IConfiguration configuration) =>
        environment.IsDevelopment() && !IsAppService(configuration);

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!IsAppService(configuration) ||
            !bool.TryParse(configuration["WEBSITE_AUTH_ENABLED"], out var enabled) || !enabled ||
            !Guid.TryParse(configuration["EasyAuth:TenantId"], out var tenantId) ||
            !Guid.TryParse(configuration["EasyAuth:ClientId"], out _) ||
            !Uri.TryCreate(configuration["EasyAuth:Audience"], UriKind.Absolute, out _))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var header = Request.Headers["X-MS-CLIENT-PRINCIPAL"];
        if (header.Count != 1 || string.IsNullOrWhiteSpace(header[0]))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            using var principal = JsonDocument.Parse(Convert.FromBase64String(header[0]!));
            var root = principal.RootElement;
            if (!root.TryGetProperty("auth_typ", out var authenticationType) || authenticationType.GetString() != "aad" ||
                !root.TryGetProperty("claims", out var claimArray) || claimArray.ValueKind != JsonValueKind.Array)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid platform identity."));
            }

            var claims = new List<Claim>();
            foreach (var claim in claimArray.EnumerateArray())
            {
                if (!claim.TryGetProperty("typ", out var type) || type.ValueKind != JsonValueKind.String ||
                    !claim.TryGetProperty("val", out var value) || value.ValueKind != JsonValueKind.String)
                {
                    return Task.FromResult(AuthenticateResult.Fail("Invalid platform claims."));
                }

                claims.Add(new Claim(type.GetString()!, value.GetString()!));
            }

            var tenant = claims.FirstOrDefault(claim => claim.Type is "tid" or "http://schemas.microsoft.com/identity/claims/tenantid")?.Value;
            var subject = claims.FirstOrDefault(claim => claim.Type is "oid" or "http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            if (!Guid.TryParse(tenant, out var actualTenant) || actualTenant != tenantId || !Guid.TryParse(subject, out _))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid platform tenant or subject."));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
        catch (Exception exception) when (exception is FormatException or JsonException or InvalidOperationException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid platform identity."));
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Response.WriteAsJsonAsync(new { error = "Authentication is required." }, Context.RequestAborted);
    }
}