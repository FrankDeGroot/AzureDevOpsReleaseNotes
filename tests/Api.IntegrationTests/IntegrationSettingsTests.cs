namespace Api.IntegrationTests;

public sealed class IntegrationSettingsTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("https://example.test/api", "test-token")]
    public void Settings_use_bearer_headers_without_query_credentials(string? url, string? token)
    {
        var values = ValidSettings();
        values["RELEASE_NOTES_API_URL"] = url;
        values["RELEASE_NOTES_API_TOKEN"] = token;
        values["FUNCTION_KEY"] = "obsolete";
        var settings = IntegrationSettings.Parse(values.GetValueOrDefault);
        using var client = settings.CreateClient();
        Assert.Equal(url ?? IntegrationSettings.DefaultApiBaseUrl, client.BaseAddress!.ToString().TrimEnd('/'));
        Assert.Empty(new Uri(client.BaseAddress, "releases").Query);
        Assert.False(client.DefaultRequestHeaders.Contains("x-functions-key"));
        Assert.Equal(token, client.DefaultRequestHeaders.Authorization?.Parameter);
        Assert.Equal(token is null ? null : "Bearer", client.DefaultRequestHeaders.Authorization?.Scheme);
        Assert.DoesNotContain("test-token", settings.ToString());
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("https://example.test/api?code=secret")]
    [InlineData("https://user:secret@example.test/api")]
    public void Invalid_base_urls_fail_without_exposing_values(string url)
    {
        var values = ValidSettings();
        values["RELEASE_NOTES_API_URL"] = url;
        var exception = Assert.Throws<InvalidOperationException>(() => IntegrationSettings.Parse(values.GetValueOrDefault));
        Assert.Contains("RELEASE_NOTES_API_URL", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
    }

    [Fact]
    public void Missing_live_settings_fail_clearly()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => IntegrationSettings.Parse(_ => null));
        Assert.Contains("AZURE_DEVOPS_BUILD_ID", exception.Message);
        Assert.Contains("Sync-Environment", exception.Message);
    }

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        ["AZURE_DEVOPS_ORGANIZATION"] = "test",
        ["AZURE_DEVOPS_PROJECT"] = "project",
        ["AZURE_DEVOPS_PROJECT_ID"] = "project-id",
        ["AZURE_DEVOPS_REPOSITORY_ID"] = "repository-id",
        ["AZURE_DEVOPS_BUILD_ID"] = "42"
    };
}