using Microsoft.Playwright;
using System.Collections.Concurrent;

namespace Client.BrowserTests;

[Trait("Category", "Integration")]
public sealed class ReleaseBrowserWorkflowTests : IAsyncLifetime
{
    private BrowserTestSettings settings = null!;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IPage? page;
    private BrowserApiStub? apiStub;
    private readonly ConcurrentQueue<string> networkRequests = new();
    private readonly ConcurrentQueue<string> browserErrors = new();

    public async Task InitializeAsync()
    {
        settings = BrowserTestSettings.FromEnvironment();
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        page = await browser.NewPageAsync();
        page.Request += (_, request) => networkRequests.Enqueue(request.Url);
        page.PageError += (_, error) => browserErrors.Enqueue(error);
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                browserErrors.Enqueue(message.Text);
            }
        };

        if (settings.ApiMode == BrowserApiMode.Stub)
        {
            apiStub = new BrowserApiStub();
            await apiStub.InstallAsync(page);
        }
    }

    public async Task DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.CloseAsync();
        }

        playwright?.Dispose();
    }

    [Fact]
    public async Task Dashboard_OpensReleaseDetailsAndReturnsToList()
    {
        var response = await page!.GotoAsync(settings.BaseUrl.ToString());
        Assert.NotNull(response);
        Assert.True(response.Ok, $"Client returned HTTP {(int)response.Status}.");

        var header = page.Locator(".list-header");
        await header.WaitForAsync();
        var headerText = (await header.InnerTextAsync()).ToUpperInvariant();
        Assert.Contains("BUILD", headerText);
        Assert.Contains("PROJECT", headerText);
        Assert.Contains("SHIPPED", headerText);
        Assert.Contains("CHANGE", headerText);

        var firstRelease = page.Locator(".release-row").First;
        await firstRelease.WaitForAsync();
        var columns = firstRelease.Locator("> span");
        Assert.Equal(5, await columns.CountAsync());

        var buildNumber = (await columns.Nth(0).InnerTextAsync()).Trim();
        var project = (await columns.Nth(1).InnerTextAsync()).Trim();
        var shippedDate = (await columns.Nth(2).InnerTextAsync()).Trim();
        var changes = (await columns.Nth(3).InnerTextAsync()).Trim();
        Assert.False(string.IsNullOrWhiteSpace(buildNumber));
        Assert.False(string.IsNullOrWhiteSpace(project));
        Assert.Matches(@"^\d+ \w+ \d{4}$", shippedDate);
        Assert.Matches(@"^\d+ work items / \d+ commits$", changes);

        if (apiStub is not null)
        {
            Assert.Equal("browser-test-2026.09.18.1", buildNumber);
            Assert.Equal("Browser Test Project", project);
            Assert.Equal("18 Sep 2026", shippedDate);
            Assert.Equal("1 work items / 1 commits", changes);
        }

        await firstRelease.ClickAsync();
        Assert.Contains("/releases/", page.Url);
        var detailHeading = page.Locator(".detail-heading h1");
        try
        {
            await detailHeading.WaitForAsync(new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            var body = (await page.Locator("body").InnerTextAsync()).Trim();
            var requests = apiStub is null ? "live mode" : string.Join(", ", apiStub.RequestedPaths);
            var apiRequests = string.Join(", ", networkRequests.Where(url => url.Contains("/api/", StringComparison.OrdinalIgnoreCase)));
            throw new Xunit.Sdk.XunitException($"Release details did not render at {page.Url}. Stub requests: {requests}. API URLs: {apiRequests}. Browser errors: {string.Join(" | ", browserErrors)}. Page text: {body}");
        }

        Assert.Equal(buildNumber, (await detailHeading.InnerTextAsync()).Trim(), StringComparer.OrdinalIgnoreCase);
        await page.GetByRole(AriaRole.Heading, new() { Name = "Work items" }).WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Commit history" }).WaitForAsync();

        var workItems = page.Locator(".work-item");
        var commits = page.Locator(".commit-item");
        Assert.True(await workItems.CountAsync() > 0, "Release details should contain work items.");
        Assert.True(await commits.CountAsync() > 0, "Release details should contain commits.");

        if (apiStub is not null)
        {
            Assert.Contains("Validate browser release view", await workItems.First.InnerTextAsync());
            Assert.Contains("Add seeded browser release data", await commits.First.InnerTextAsync());
        }

        await page.GetByRole(AriaRole.Link, new() { Name = "← All releases" }).ClickAsync();
        await page.Locator(".release-row").First.WaitForAsync();

        if (apiStub is not null)
        {
            Assert.Equal(2, apiStub.RequestedPaths.Count(path => path == "/api/releases"));
            Assert.Single(apiStub.RequestedPaths, path => path == $"/api/releases/{BrowserApiStub.ReleaseId}");
        }
        else
        {
            var apiUrls = networkRequests.Where(url => url.Contains("/api/releases", StringComparison.Ordinal)).ToArray();
            Assert.Contains(apiUrls, url => new Uri(url).AbsolutePath == "/api/releases");
            Assert.Contains(apiUrls, url => new Uri(url).AbsolutePath.StartsWith("/api/releases/", StringComparison.Ordinal));
            Assert.All(apiUrls, url => Assert.Equal(settings.BaseUrl.Authority, new Uri(url).Authority));
        }
    }

    [Fact]
    public async Task Theme_FollowsSystemColorPreferenceAndUpdatesWhenItChanges()
    {
        await page!.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Light });
        var response = await page.GotoAsync(settings.BaseUrl.ToString());
        Assert.NotNull(response);
        Assert.True(response.Ok, $"Client returned HTTP {(int)response.Status}.");

        Assert.Equal("light", await page.EvaluateAsync<string>("document.documentElement.dataset.bsTheme"));
        Assert.Equal("#f3f0e8", await page.EvaluateAsync<string>("getComputedStyle(document.documentElement).getPropertyValue('--paper').trim()"));

        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.WaitForFunctionAsync("document.documentElement.dataset.bsTheme === 'dark'");

        Assert.Equal("#171916", await page.EvaluateAsync<string>("getComputedStyle(document.documentElement).getPropertyValue('--paper').trim()"));
    }
}