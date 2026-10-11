namespace Client.BrowserTests;

[Trait("Category", "Integration")]
public sealed class BrowserTestSettingsTests
{
    [Fact]
    public void UnsetValues_DefaultToLocalClientAndLiveApi()
    {
        var settings = BrowserTestSettings.Parse(null, null);

        Assert.Equal(new Uri("http://localhost:4280"), settings.BaseUrl);
        Assert.Equal(BrowserApiMode.Live, settings.ApiMode);
    }

    [Fact]
    public void LiveMode_UsesConfiguredClientUrl()
    {
        var settings = BrowserTestSettings.Parse("https://release-notes.example.test", "live");

        Assert.Equal(new Uri("https://release-notes.example.test"), settings.BaseUrl);
        Assert.Equal(BrowserApiMode.Live, settings.ApiMode);
    }

    [Fact]
    public void StubMode_IsSelectedOnlyExplicitly()
    {
        var settings = BrowserTestSettings.Parse(null, "stub");

        Assert.Equal(BrowserApiMode.Stub, settings.ApiMode);
    }

    [Fact]
    public void InvalidMode_FailsWithConfigurationNameAndAllowedValues()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => BrowserTestSettings.Parse(null, "automatic"));

        Assert.Contains("CLIENT_API_MODE", exception.Message);
        Assert.Contains("'live' or 'stub'", exception.Message);
    }
}