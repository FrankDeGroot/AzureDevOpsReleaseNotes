# Design

## Context

The client is a standalone Blazor WebAssembly app. Its `HttpClient` uses the hosting origin, and `ReleaseNotesApiClient` requests `/api/releases` and `/api/releases/{id}`. The list and detail API functions allow anonymous access; compiling is function-key protected. Locally, Static Web Apps CLI serves the client at `http://localhost:4280` and proxies `/api` to the Functions host at `http://localhost:7071`. The existing API integration suite runs before the client deployment in GitHub Actions and compiles a release record that the browser suite can inspect.

## Goals / Non-Goals

**Goals:**
- Exercise the built Blazor client in Chromium using a real browser and its actual rendered DOM and routes.
- Preserve same-origin API behavior in live mode, locally through SWA CLI and in CI through the deployed Static Web App.
- Offer deterministic API response stubs for explicitly selected UI-only runs.

**Non-Goals:**
- Change client runtime API configuration or API authorization.
- Replace the existing API integration tests or validate Azure DevOps/Cosmos behavior from the browser suite.
- Add broad cross-browser coverage in the initial suite.

## Decisions

### Playwright for .NET with xUnit

Use the Playwright .NET browser automation library in a separate `tests/Client.BrowserTests` project, following the existing xUnit test conventions. Playwright is appropriate here because the behavior under test includes WebAssembly startup, browser routing, and user navigation; component-only tests would not cover those browser boundaries.

### External client host, configurable test mode

The test runner targets the client using `CLIENT_BASE_URL`, defaulting to the existing local SWA CLI URL `http://localhost:4280`. The host remains external to the test process: locally, developers start the Client, Functions host, and SWA CLI as documented; CI targets the deployed Static Web App after the existing deploy action completes.

The test-runner setting `CLIENT_API_MODE` controls only Playwright network routing. Unset or `live` leaves API requests untouched, so they reach the same-origin API. `stub` intercepts only matching `/api/releases` requests and returns deterministic list/detail fixtures. Unknown values fail configuration validation. A failed live request remains a test failure; it never enables stubs.

### CI sequencing

Keep browser tests out of the existing pre-deploy test command by retaining the `Category!=Integration` filter. Run them after `Azure/static-web-apps-deploy@v1`, targeting the Static Web App hostname already emitted by the infrastructure deployment. Install Chromium in the runner before invoking the browser-test project. The existing API integration step runs earlier and compiles the release data required by the live browser assertions.

### Initial browser coverage

Use a representative release response to assert dashboard columns and counts, open its row, assert work-item and commit sections on the detail page, then return to the dashboard. Run this workflow in live mode by default; use the same user flow in stub mode to verify that isolated rendering and navigation do not require API availability.

## Risks / Trade-offs

- **Live data can be empty or unstable.** The deployed API integration suite compiles a known test build before the browser suite; local live runs should run the API integration suite or otherwise ensure a release exists.
- **Browser installation adds CI setup time.** Install only Chromium for the initial scope.
- **A deployed browser run depends on SWA/API availability.** Keep the test after deployment and fail it on readiness or API errors instead of masking them with stubs.

## Migration Plan

No runtime migration is required. Add the test project to the solution, install its Playwright browser in CI, run the live suite after client deployment, and document local live and explicit stub commands. If the browser step must be rolled back, remove that workflow step; it does not affect publishing or runtime behavior.