# Tasks

## 1. Combined ASP.NET Core Host

- [ ] 1.1 Convert `src/Api/Api.csproj` and `Program.cs` from the Functions worker to the .NET 10 Web SDK, preserve domain-service registrations and credential selection, and remove worker-only package/build settings; verify `dotnet build src/Api/Api.csproj` succeeds without generating a Functions extension project.
- [ ] 1.2 Replace HTTP Function adapters with minimal endpoints for compile/list/detail/health, preserving JSON casing, errors, project defaults, cancellation, and the ten-second health timeout; add focused cases to `tests/Api.Tests` and verify valid/malformed/empty requests, filtering, missing records, and healthy/unhealthy responses with isolated services.
- [ ] 1.3 Add standard hosted-Blazor client/static-asset publish integration and routing that excludes API and missing-asset paths from fallback; verify host tests cover root, direct detail navigation, framework assets, unknown API paths, JSON errors, and missing asset 404s.
- [ ] 1.4 Replace worker telemetry with ASP.NET Core Application Insights instrumentation, retaining optional local telemetry and client configuration; verify host startup succeeds without a local connection string and configured telemetry does not alter endpoint contracts.
- [ ] 1.5 Update `docs/architecture.md` and the application component overview in `README.md`; verify both describe one ASP.NET Core host serving the existing WebAssembly client rather than a new UI runtime.

## 2. Compilation Authentication

- [ ] 2.1 Implement the Easy Auth principal adapter and compile-only authorization, with trust restricted to the configured App Service boundary and missing/malformed configuration failing closed; verify focused tests reject anonymous, spoofed-header, malformed-principal, and legacy-key-only requests without calling domain services.
- [ ] 2.2 Restrict anonymous compilation to local Development with no App Service hosting markers; verify the environment matrix permits local Development but rejects anonymous non-Development and Azure-hosted Development requests.
- [ ] 2.3 Add test-only authenticated-principal injection and verify authenticated compile retains the existing HTTP 200 contract while anonymous viewer/list/detail/health routes remain public; verify no test handler is enabled by production environment settings or request headers.
- [ ] 2.4 Document the authentication boundary, local exception, Function-key breaking change, and separation of inbound identity from outbound Azure DevOps credentials in `docs/run-the-app.md` and `docs/integration-tests.md`; verify the instructions neither expose tokens to the browser nor suggest self-supplied Easy Auth headers.

## 3. Local Runtime and Test Configuration

- [ ] 3.1 Replace Functions local settings with ASP.NET Core configuration and a Development launch profile at `http://localhost:7071`; verify one `dotnet run --project src/Api` command serves the viewer and API without SWA or Functions processes and static delivery does not eagerly contact Cosmos or Azure DevOps.
- [ ] 3.2 Replace `FunctionKey` and the query handler in `tests/Api.IntegrationTests/IntegrationSettings.cs` with optional `RELEASE_NOTES_API_TOKEN` bearer-header support; add settings tests verifying default/local/custom URLs, optional tokens, and absence of credential query strings.
- [ ] 3.3 Update browser-test defaults and assertions to the combined host while retaining explicit stub/live modes; verify `BrowserTestSettingsTests` and isolated browser workflow tests pass against that host, including a refreshed release-detail URL.
- [ ] 3.4 Update `.env.example.ps1` and `scripts/Sync-Environment.ps1` for the auth client ID/audience and local-only API token, retaining obsolete `FUNCTION_KEY` only in the skip list; verify placeholder-only examples and a controlled sync run prove URL/token/legacy-key values are not pushed or printed.
- [ ] 3.5 Update `docs/run-the-app.md`, `docs/local-emulators.md`, `docs/environment-configuration.md`, `docs/integration-tests.md`, `docs/client-browser-tests.md`, and README setup links with clean-rebuild/bootstrap/retry steps and separate isolated/live test commands; verify examples use one host, Linux-local browser-test output, optional credentials for isolated checks, and Cosmos plus Azure CLI sign-in only for real dependency checks.
- [ ] 3.6 Update `.devcontainer/devcontainer.json` to forward and label port 7071, explicitly provide .NET 10/PowerShell 7 and retained CLI tooling, remove obsolete Functions tooling/recommendations, and regenerate feature lock metadata only if declarations change; verify configuration parses and a rebuilt container reports the required tool versions without relying on earlier manual installs.
- [ ] 3.7 Update `.devcontainer/compose.yaml` and the existing Cosmos trust hook to remove Functions-only Azurite/storage dependencies while preserving Cosmos networking, volume, readiness, and repeatable certificate trust; verify the Compose model resolves, restart/rebuild preserves emulator data, HTTPS access succeeds without TLS bypass, and readiness/trust failures report bounded actionable errors.
- [ ] 3.8 Add repeatable dev-container bootstrap for solution restore, browser-test build, package-matched Chromium, and Linux browser libraries, using `/tmp/client-browser-tests-build/` or equivalent Linux-local output; verify Chromium launches as the development user, rerunning bootstrap succeeds, and lifecycle hooks neither require credentials nor execute live tests.
- [ ] 3.9 Rebuild the dev container from committed configuration and follow the documented isolated verification path; verify restore/build/Release publish, application access through forwarded port 7071, unit tests, and explicit stub-browser tests succeed without Azure sign-in or live external requests, including on a Windows-backed workspace mount when available; record unavailable rebuild or mount coverage as blocked rather than claiming success.

## 4. App Service Infrastructure and Identity Setup

- [ ] 4.1 Replace SWA/Functions resources in `infra/workload.bicep` with the new Linux web app and separate B1-default configurable plan, HTTPS, Always On, Production, system identity, Cosmos settings, and monitoring; verify Bicep compilation and template assertions show no new SWA, Function App, Functions storage, or replacement Cosmos account.
- [ ] 4.2 Add required Entra tenant/API client/audience parameters and Easy Auth settings that validate tokens but permit anonymous platform access; verify compiled configuration enables the single-tenant provider and audiences without globally requiring sign-in, and empty required configuration is rejected before publishing.
- [ ] 4.3 Propagate web-app outputs and parameters through `infra/main.bicep` and `infra/main.bicepparam`; verify both Bicep entry points compile and output references match the web app while Cosmos and monitoring resource names remain unchanged.
- [ ] 4.4 Generalize the Function-specific name and identity lookup in `scripts/Initialize-AzureDevOpsTestProject.ps1` to the new web app without altering project access semantics; verify mocked CLI responses cover successful lookup, missing identity, and repeat/idempotent grant handling.
- [ ] 4.5 Update provisioning, deployment setup, and Azure DevOps test-project guides with B1 cost/runtime prerequisites, new outputs, Entra registration/consent requirements, and outbound identity grants; verify documented parameters match the templates and setup script.

## 5. Single-Artifact CI/CD

- [ ] 5.1 Replace `.github/workflows/azure-static-web-apps.yml` with an App Service workflow using federation, pre-publish tests, incremental infrastructure deployment, one telemetry-configured publish, and one web-app deployment; verify workflow syntax and remove SWA token retrieval, Function key retrieval, separate deployments, and SWA PR-close cleanup.
- [ ] 5.2 Keep pull requests validation-only and main/manual events deployable, and include workflow/configuration changes in path triggers; verify the event/condition matrix prevents production mutations from PR jobs and still runs required local/isolated checks.
- [ ] 5.3 Acquire and mask a short-lived API-audience token from the federated identity, pass it only to server-side tests, and run API checks against the web-app output URL after public list warmup; verify no ARM token, Function key, query credential, or browser-visible token is used.
- [ ] 5.4 Extend deployed API checks for anonymous compile rejection, invalid/expired/wrong-audience token rejection, spoofed identity input, public reads, and existing compile/read-back/idempotency/health behavior; verify the workflow fails on a rejected valid caller or any failed assertion and runs live browser tests against the same origin.
- [ ] 5.5 Update `docs/configure-azure-oidc-for-github-actions.md`, `docs/deploy-with-github-actions.md`, and deployment documentation with caller permissions/consent, API token acquisition, workflow outputs, and the loss of SWA preview environments; verify examples consistently use the web-app URL and audience.

## 6. Migration Runbook and End-to-End Verification

- [ ] 6.1 Deliver an operator-run cutover/rollback/retirement section in `docs/deployment.md` covering resource inventory, region/SKU/cost checks, side-by-side deployment, new identity grants, caller migration, a default seven-day rollback window, and explicit deletion approval; verify it never deletes the resource group, Cosmos, or shared monitoring and explains incremental deployment leaves legacy resources intact.
- [ ] 6.2 From the rebuilt dev container, run the solution's non-integration checks, strict OpenSpec validation, and a Release publish; start the published output independently of source-tree assets and verify root/framework assets, same-origin API behavior, direct detail navigation, API/missing-asset errors, and the Production anonymous-compile rejection.
- [ ] 6.3 From the same dev container, run local real-dependency API integration tests and live browser tests against the combined Development host with ready/trusted Cosmos and the developer Azure DevOps identity; verify the compile/read-back/list/idempotency and viewer navigation workflow passes, recording missing credentials or dependencies as blocked rather than success.

## Workflow follow-up

- Azure provisioning, caller cutover, and resource deletion require separate operator authorization; implementing this change does not itself authorize those operations.
- After authorized deployment, record passing Easy Auth boundary checks, dependency/API integration tests, and live browser checks before moving callers. Keep the old deployment during the agreed rollback window.
- After the window and explicit approval, retire only inventoried legacy-only hosts/storage/roles and obsolete identity grants, then rerun the new-host smoke checks.
- Archive this change only after implementation review and required verification gates are satisfied; retain any pending operator-run migration steps in the delivered runbook.