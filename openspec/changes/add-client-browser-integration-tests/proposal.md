# Proposal

## Why

The existing integration suite verifies the Functions API directly, but it does not exercise the Blazor WebAssembly app in a browser. A browser suite will cover startup, rendered release data, and navigation through the real client/API path while retaining an explicit stub mode for isolated UI checks.

## What Changes

- Add a Playwright for .NET browser test suite using the repository's xUnit conventions.
- Make live API requests the default; allow stubbed API responses only when an explicit test setting selects stub mode. Live failures must fail the test rather than silently switching modes.
- Cover the releases dashboard and navigation to and from release details in a real browser.
- Run the browser suite against the deployed Static Web App after client deployment, and document local prerequisites and commands.

## Capabilities

### New Capabilities
- `client-browser-testing`: Real-browser verification of the Blazor client with live API behavior by default and explicitly selected API stubs for isolated checks.

### Modified Capabilities
None. The existing API integration capability remains unchanged.

## Impact

- Adds a .NET test project and Playwright browser dependency.
- Updates `ReleaseNotes.slnx`, the GitHub Actions workflow, and README testing instructions.
- No runtime API or client behavior changes are intended.