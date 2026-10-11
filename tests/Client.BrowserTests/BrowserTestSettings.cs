namespace Client.BrowserTests;

internal enum BrowserApiMode
{
    Live,
    Stub
}

internal sealed record BrowserTestSettings(Uri BaseUrl, BrowserApiMode ApiMode)
{
    internal static BrowserTestSettings FromEnvironment() => Parse(
        Environment.GetEnvironmentVariable("CLIENT_BASE_URL"),
        Environment.GetEnvironmentVariable("CLIENT_API_MODE"));

    internal static BrowserTestSettings Parse(string? baseUrl, string? apiMode)
    {
        var configuredBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:4280" : baseUrl;
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var parsedBaseUrl) ||
            (parsedBaseUrl.Scheme != Uri.UriSchemeHttp && parsedBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("CLIENT_BASE_URL must be an absolute HTTP or HTTPS URL.");
        }

        var configuredApiMode = string.IsNullOrWhiteSpace(apiMode) ? "live" : apiMode;
        var parsedApiMode = configuredApiMode.ToLowerInvariant() switch
        {
            "live" => BrowserApiMode.Live,
            "stub" => BrowserApiMode.Stub,
            _ => throw new InvalidOperationException($"CLIENT_API_MODE must be 'live' or 'stub'; received '{configuredApiMode}'.")
        };

        return new BrowserTestSettings(parsedBaseUrl, parsedApiMode);
    }
}