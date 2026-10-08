namespace Api.IntegrationTests;

public sealed record IntegrationSettings(
    Uri ApiBaseUrl,
    string? FunctionKey,
    string Organization,
    string Project,
    string ProjectId,
    string RepositoryId,
    int BuildId)
{
    public const string DefaultApiBaseUrl = "http://localhost:7071/api";

    public static IntegrationSettings FromEnvironment()
    {
        var missing = new List<string>();
        string Required(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
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

        var baseUrl = Environment.GetEnvironmentVariable("RELEASE_NOTES_API_URL");
        baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultApiBaseUrl : baseUrl;
        var functionKey = Environment.GetEnvironmentVariable("FUNCTION_KEY");

        return new IntegrationSettings(
            new Uri(baseUrl.TrimEnd('/') + "/"),
            string.IsNullOrWhiteSpace(functionKey) ? null : functionKey,
            organization,
            project,
            projectId,
            repositoryId,
            buildId);
    }

    public HttpClient CreateClient()
    {
        HttpMessageHandler handler = new HttpClientHandler();
        if (FunctionKey is not null)
        {
            handler = new FunctionKeyHandler(FunctionKey) { InnerHandler = handler };
        }

        return new HttpClient(handler) { BaseAddress = ApiBaseUrl, Timeout = TimeSpan.FromSeconds(100) };
    }

    // The Static Web App proxy drops the x-functions-key header but forwards the code query parameter.
    private sealed class FunctionKeyHandler(string functionKey) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = new UriBuilder(request.RequestUri!);
            var code = $"code={Uri.EscapeDataString(functionKey)}";
            uri.Query = string.IsNullOrEmpty(uri.Query) ? code : $"{uri.Query.TrimStart('?')}&{code}";
            request.RequestUri = uri.Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
