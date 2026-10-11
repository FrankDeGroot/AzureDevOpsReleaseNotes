# Tasks

## 1. Browser test project

- [x] 1.1 Create `tests/Client.BrowserTests` as a .NET 10 xUnit project with the Playwright .NET dependency, and add it to `ReleaseNotes.slnx`; verify restore and build succeed.
- [x] 1.2 Categorize the browser tests as `Integration` and verify `dotnet test --filter "Category!=Integration"` excludes the browser suite while a category-filtered run selects it.

## 2. Browser modes and release workflow

- [x] 2.1 Add test settings for `CLIENT_BASE_URL` and `CLIENT_API_MODE`, defaulting to the local SWA CLI URL and live API mode; verify unset/live settings leave API requests unmocked and invalid mode values fail clearly.
- [x] 2.2 Add Playwright API-route fixtures that respond to release list and detail requests only in explicit stub mode; verify stubbed requests use deterministic data and do not reach the live API.
- [x] 2.3 Add a real-browser test for dashboard release columns, opening a release, validating work items and commits, and returning to the list; verify it passes in stub mode and in live mode against a seeded local app.
- [x] 2.4 Install Chromium using the generated Playwright install script as part of the documented local test setup; verify the browser suite launches successfully after installation.

## 3. CI and documentation

- [x] 3.1 Add a GitHub Actions browser-test step after `Azure/static-web-apps-deploy@v1`, set `CLIENT_API_MODE=live` and the deployed `CLIENT_BASE_URL`, install Chromium, and run the categorized suite; verify the workflow syntax and that the browser step fails on test failure.
- [x] 3.2 Document browser-test prerequisites, local live API setup, the explicit `CLIENT_API_MODE=stub` command, and the live default in the README; verify each documented command matches the test project and local SWA CLI topology.

## 4. Integration verification

- [x] 4.1 Run `openspec validate add-client-browser-integration-tests --strict` and verify every planning artifact passes validation.