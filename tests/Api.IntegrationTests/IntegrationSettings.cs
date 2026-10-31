namespace Api.IntegrationTests;

public sealed record IntegrationSettings(
    Uri ApiBaseUrl,
    string? ApiToken,
    string Organization,
    string Project,
    string ProjectId,
    string RepositoryId,
    int BuildId)
{
    public const string DefaultApiBaseUrl = "http://localhost:7071/api";

    public static IntegrationSettings FromEnvironment() => Parse(Environment.GetEnvironmentVariable);

    public static IntegrationSettings Parse(Func<string, string?> getSetting)
    {
        var missing = new List<string>();
        string Required(string name)
        {
            var value = getSetting(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(name);
            }

            return value ?? string.Empty;
        }

        var organization = Required("AZURE_DEVOPS_ORGANIZATION");
        var project = Required("AZURE_DEVOPS_PROJECT");
        var projectId = Required("AZURE_DEVOPS_PROJECT_ID");
        var repositoryId = Required("AZURE_DEVOPS_REPOSITORY_ID");
        var buildIdText = Required("AZURE_DEVOPS_BUILD_ID");
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing integration test settings: {string.Join(", ", missing)}. " +
                "Run scripts/Initialize-AzureDevOpsTestProject.ps1 and scripts/Sync-Environment.ps1, then load .env.ps1.");
        }

        if (!int.TryParse(buildIdText, out var buildId) || buildId <= 0)
        {
            throw new InvalidOperationException("AZURE_DEVOPS_BUILD_ID must be a positive integer.");
        }

        var baseUrl = getSetting("RELEASE_NOTES_API_URL");
        baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultApiBaseUrl : baseUrl;
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var apiUri) ||
            (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(apiUri.Query) || !string.IsNullOrEmpty(apiUri.UserInfo) || !string.IsNullOrEmpty(apiUri.Fragment))
        {
            throw new InvalidOperationException("RELEASE_NOTES_API_URL must be an HTTP(S) URL without credentials, query, or fragment.");
        }
        var apiToken = getSetting("RELEASE_NOTES_API_TOKEN");

        return new IntegrationSettings(
            apiUri,
            string.IsNullOrWhiteSpace(apiToken) ? null : apiToken,
            organization,
            project,
            projectId,
            repositoryId,
            buildId);
    }

    public HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = ApiBaseUrl, Timeout = TimeSpan.FromSeconds(100)
        };
        if (ApiToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiToken);
        }

        return client;
    }

    public override string ToString() => $"IntegrationSettings {{ ApiBaseUrl = {ApiBaseUrl}, BuildId = {BuildId} }}";
}
