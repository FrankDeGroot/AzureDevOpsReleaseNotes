# Design

## Context

See `proposal.md` for motivation and the capability deltas for behavior contracts. The current implementation has four HTTP Functions and no observed non-HTTP triggers. `ReleaseFunctions.cs` owns compile/list/detail request handling; `HealthFunctions.cs` applies a ten-second dependency-check timeout. Domain services and shared models are already independent of the Functions request types.

`infra/workload.bicep` provisions Standard SWA, a linked Linux Flex Consumption Function App, a Functions storage account and roles, Cosmos DB, and monitoring. The workflow publishes API and client separately, retrieves a Function key, and runs API requests through SWA. `src/Client/Program.cs` already uses same-origin HTTP requests.

Local startup currently uses three processes and SWA on port 4280; API integration tests default to 7071. The dev container starts Azurite solely for Functions storage as well as Cosmos. The Azure DevOps setup script discovers a `func-release-notes-*` identity. Existing specs explicitly require the old hosting and key-based test flow; the deltas intentionally replace those requirements.

The current dev-container bootstrap globally installs SWA CLI and Functions Core Tools, but does not explicitly prepare PowerShell or Playwright. Its existing Cosmos trust hook waits for readiness and installs the current emulator certificate. The browser-test guide documents a Windows-mounted workspace limitation requiring generated browser-test output on a Linux filesystem; preserve that workaround in the revised bootstrap and commands.

User decisions: one ASP.NET Core web app; keep the existing Blazor WebAssembly client; Easy Auth protects compilation only; local Development permits anonymous compilation; retire old hosts only after verification.

## Goals / Non-Goals

**Goals:**

- Reuse `src/Api`, domain services, `src/Client`, and shared models rather than creating another server project.
- Preserve JSON contracts, IDs, Cosmos partitioning, compiler idempotency, and dependency health status semantics across the hosting change.
- Establish a clear trust boundary for Easy Auth identity and a bypass that cannot apply in Azure.
- Make the published artifact, not only development asset discovery, testable before Azure deployment.

**Non-Goals:**

- No Blazor Server/SSR conversion, UI redesign, new persistence format, or data migration.
- No user authentication for the public viewer/read APIs and no custom login UI.
- No replacement of outbound Azure DevOps managed identity with the inbound caller token.
- No production container hosting, custom domains, deployment slots, or automatic destructive resource cleanup. The development container remains supported. SWA PR preview environments are not recreated; PRs validate without publishing to production.

## Decisions

### 1. Convert the API project to an ASP.NET Core host

Change `src/Api/Api.csproj` to the Web SDK targeting .NET 10. Replace the Functions worker bootstrap and trigger adapters with minimal HTTP endpoints under `/api`, using the existing services through dependency injection. Remove Functions worker packages, generated worker-extension settings, `host.json`, and Functions-only local settings once their replacement configuration is in place.

Retain the four existing routes and their request/response semantics: compile validates JSON and required metadata, list filters by project, detail supports `projectId` and JSON 404 errors, and health returns 200 or 503 with its ten-second timeout. Preserve wire serialization intentionally; do not assume Functions and ASP.NET Core defaults are equivalent. Carry cancellation through the service calls. Use ASP.NET Core Application Insights server instrumentation in place of worker instrumentation and retain existing client telemetry configuration.

Alternative: keep the Functions runtime and add static-file serving. Rejected because it retains a Functions-specific runtime and deployment contract rather than producing the requested ordinary web app. Creating a second web host was also rejected because it duplicates the existing API ownership boundary.

### 2. Use the standard hosted-Blazor publish model

Use the standard server-to-client project reference and matching stable .NET 10 Blazor WebAssembly hosting packages/static-web-assets integration. Keep the client's static asset mode suitable for referenced hosting. Do not set `ReferenceOutputAssembly=false`: implementation checks showed it drops transitive library static assets, including the Application Insights initializer, from the server manifest. The client remains WebAssembly; the project reference does not introduce server-side UI rendering. One `dotnet publish src/Api/Api.csproj -c Release` must include the complete client runtime and assets; do not rely on an untracked copy command or a separately deployed client output.

Serve framework files and static assets before the client-route fallback. Reserve `/api` from that fallback so unknown API routes and operation failures remain API responses. Restrict client fallback to navigational paths so missing scripts, configuration, and runtime assets remain 404. Remove the SWA-specific routing file when server routing replaces it. The client keeps its existing same-origin base address.

Alternative: explicitly merge two unrelated publish directories in CI. Rejected because it makes local publishing differ from deployment and is easier to break when client asset layout changes.

### 3. Use Easy Auth validation plus endpoint-level authorization

Configure `Microsoft.Web/sites/config` `authsettingsV2` with Microsoft Entra enabled, a single-tenant issuer, the API application's client ID, and explicit allowed audiences. Allow anonymous requests through the platform globally: it must not redirect or challenge the viewer, reads, or health. The compile endpoint alone applies authorization and returns JSON HTTP 401 for a missing principal.

Easy Auth validates inbound tokens and supplies platform identity to the application. Add a small ASP.NET Core authentication adapter for its principal claims and reject malformed/missing identity. Trust these headers only behind the Azure App Service authentication boundary, where platform-supplied headers replace external identity headers; they are not a standalone authentication protocol. No direct backend ingress bypassing that boundary is introduced. Validate deployment configuration as a gate and fail closed for compilation if trust settings are absent. Tests may inject an isolated test authentication handler, but that handler must not be selectable through production configuration or headers.

The anonymous bypass requires both Development and absence of the App Service hosting marker (`WEBSITE_INSTANCE_ID`/`WEBSITE_SITE_NAME`); an Azure app named Development must not bypass authentication. Azure deployment explicitly sets Production. A non-Development local host without Easy Auth may serve public endpoints but cannot authenticate compilation from self-supplied headers.

Automation obtains an access token for the dedicated API audience using an authorized Entra workload identity. Prepare the API app registration, application permissions/consent needed for app-only callers, and caller federation before enabling deployment tests; these directory operations are documented prerequisites, not implicit Bicep side effects. The GitHub Azure login identity and each Azure DevOps pipeline caller need appropriate access to request an accepted API token. Configure the API audience explicitly rather than reusing an ARM token. Do not retain Function-key compatibility.

Inbound API caller authentication and outbound dependency authentication remain separate: the server still uses its system-assigned identity for Azure DevOps, with `DefaultAzureCredential` in local Development. The Azure DevOps setup script must grant the new web app identity access before cutover.

Alternative: require platform authentication globally. Rejected because it blocks public reads. Shared keys were rejected by the user's Easy Auth decision; implementing independent bearer validation was rejected because it duplicates the platform boundary.

### 4. Provision one Linux web app on a dedicated plan

Use a new `app-release-notes-${resourceNameSuffix}` web app on a new `asp-release-notes-web-${resourceNameSuffix}` Linux B1 plan, with the supported .NET 10 runtime, HTTPS-only, current TLS settings, Always On, system-assigned identity, Production environment, Cosmos settings, and Application Insights. B1 is the economical dedicated baseline; expose the SKU as a parameter and confirm regional runtime/SKU availability and estimated cost before provisioning. Do not try to convert the existing FC1 plan in place.

Keep the existing Cosmos account name, location, database/container configuration, and data. Keep existing monitoring names and workspace. Do not change Cosmos authentication or free-tier/serverless choices as part of this migration. Stop declaring SWA, linked backend, Function App, Functions storage, and storage roles in the steady-state template. Use incremental deployment: removing these definitions does not delete deployed resources. Expose `webAppName` and `webAppDefaultHostname` outputs through `infra/main.bicep`, plus existing relevant monitoring outputs.

Alternative: a free/shared plan or reusing FC1. Rejected because FC1 is Functions-specific and free/shared hosting is not the selected reliable dedicated baseline for this workload. A dedicated plan has standing cost even when idle.

### 5. Deploy and verify a single artifact

Replace the SWA-named workflow with an App Service workflow using federated Azure login and a web-app deployment action supported for Linux .NET. Run unit/host tests before publishing, deploy Bicep, stamp the existing public client telemetry setting, publish once, and deploy that output once. Resolve the app hostname from deployment outputs.

Obtain and mask a short-lived access token for `RELEASE_NOTES_AUTH_AUDIENCE` with the logged-in workload identity, then supply it to the integration suite as `RELEASE_NOTES_API_TOKEN`. Remove SWA token retrieval, Function key retrieval/query handlers, and PR-close SWA cleanup. PRs run restore/build/unit and isolated browser checks without mutating production; main/manual deployments run real post-deployment checks. Add workflow/configuration paths to triggers so hosting changes are not skipped.

Preserve the current Cosmos initialization ordering: a public list request can initialize the container before health verification. Run dependency health, authenticated compile/read-back/idempotency checks, anonymous compile rejection, invalid-token rejection, public-route checks, and live browser navigation against the same deployed origin. Invalid-token checks must exercise Easy Auth itself; in-process host tests alone cannot prove platform validation.

### 6. Keep local configuration simple and compatible where useful

Bind the combined Development host to `http://localhost:7071` so existing API default URLs remain valid. Change browser-test default origin and dev-container forwarding to 7071. Use normal ASP.NET Core environment variables, appsettings, and user secrets in place of Functions local settings. Retain the Cosmos emulator and developer Azure CLI credential flow. Remove Azurite, `AzureWebJobsStorage`, and the SWA/Functions CLI dependencies when no remaining consumer needs them; retain OpenSpec tooling.

Add placeholder `RELEASE_NOTES_AUTH_CLIENT_ID`, `RELEASE_NOTES_AUTH_AUDIENCE`, and optional local-only `RELEASE_NOTES_API_TOKEN` configuration. Reuse `AZURE_TENANT_ID` for tenant configuration and existing deployment credentials. Replace `FUNCTION_KEY` in examples and tests; keep it on the environment-sync local-only skip list so an old local value is not accidentally uploaded. Never commit or persist the runtime CI access token or expose it in browser configuration.

Generalize the Azure DevOps setup script's web-site name parameter and lookup to the new web app while retaining its project identity grants. Update run, emulator, environment, test, deployment, OIDC, provisioning, and architecture guides together.

### 7. Make the dev container a repeatable run-and-test environment

Retain the .NET 10 SDK workspace image and explicitly provide PowerShell 7, Azure CLI, GitHub CLI, Node/OpenSpec, and existing shell/bootstrap utilities. Remove global SWA/Functions CLI installation and obsolete Functions-specific editor recommendations. Retain the C# and Blazor tooling needed for the combined host. If feature declarations change, regenerate the feature lock metadata with the supported dev-container tooling rather than hand-editing generated hashes or unrelated lock entries.

Move port forwarding and its label to 7071. Preserve the Compose network relationship with Cosmos so the existing localhost connection and readiness endpoints remain valid; remove only Azurite, its dependency/volume declaration, and `AzureWebJobsStorage`. Do not delete the Cosmos named volume during rebuild or cleanup. Retain the public emulator-only key and normal TLS verification.

Make creation/bootstrap restore the solution and build the browser-test project before using its generated Playwright installer. Use `/tmp/client-browser-tests-build/` or an equivalent writable Linux-local output directory consistently for browser builds, installation, and test runs, including Windows-backed workspace mounts. Install Chromium matching the restored Playwright package and its required Linux libraries through repeatable, noninteractive setup. Prefer OS-level prerequisites during image/feature provisioning; browser install and restore run as the development user. Provide a documented rerunnable bootstrap command for browser-package upgrades and partial setup recovery.

Preserve the startup certificate hook's bounded readiness wait and current-certificate import, with actionable diagnostics on failure. Re-run trust setup on restart, but do not perform repeated full browser downloads or solution builds on every start. Lifecycle hooks must never sign in to Azure, request secrets, create cloud resources, or run live integration tests. A startup dependency failure must be identifiable without representing live verification as successful.

Document two verification paths. The isolated path restores/builds, starts the host in Development, runs unit tests and explicit stub-browser tests, and does not contact Cosmos or Azure DevOps. Host startup and static delivery must not eagerly connect to external dependencies solely to make those checks possible. The live path additionally requires ready/trusted Cosmos, `az login`, `.env.ps1` test-project values, and the API compile round trip before live browser checks. The default browser mode remains live; do not silently substitute stubs when credentials or dependencies are missing.

Validate the container definition and Compose model, then rebuild from the committed setup without relying on tools or browsers installed in the previous session. Verify runtime tools, forwarded app access, a Release publish, Chromium launch, isolated tests, emulator TLS access, and restart/rebuild persistence. Run real dependency checks only after the developer supplies the documented external prerequisites. Document how to retry setup and where Linux-local generated test output lives.

Alternative: leave test tooling as a series of manual host fixes. Rejected because it does not demonstrate that a rebuilt development container can run and test the changed application reproducibly. Running every test in lifecycle hooks is also rejected because it makes opening the workspace depend on Azure sign-in and a live test project.

## Risks / Trade-offs

- Easy Auth header trust depends on the platform boundary -> deploy auth settings with the app, test spoofed-header and invalid-token calls through Azure, and never trust identity headers on an ordinary local Production host.
- The new web app has a different managed identity -> grant its Azure DevOps access explicitly; test outbound calls before moving pipeline traffic.
- Entra application provisioning needs directory privileges separate from Azure RBAC -> document prerequisites, consent, API audience, token acquisition, and caller setup; fail before publishing if required configuration is missing.
- Dedicated hosting adds idle cost and one app shares CPU between API and static delivery -> document B1 pricing and provide SKU override without promising consumption economics.
- Static assets can appear locally but be missing from publish -> start and test the Release publish directory independently of the source tree.
- Sync HTTP compilation can encounter App Service request limits for large builds -> preserve current synchronous behavior and document the limit; asynchronous jobs are out of scope.
- Old URLs and Function keys stop working after retirement -> verify each caller's bearer-token flow, retain the previous endpoint during the rollback window, and require explicit cutover approval.
- Removing Bicep resources does not remove existing Azure instances -> use an inventoried, operator-run retirement procedure, never complete-mode deployment or whole-resource-group deletion.
- Browser binaries and Linux libraries can drift from the Playwright package, and Windows-backed mounts can reject executable permission changes -> install the package-matched Chromium during repeatable bootstrap and keep generated browser-test output on the Linux filesystem.
- A warm workspace can hide missing container prerequisites -> require a clean rebuild and credential-free verification path, then separately verify Cosmos trust and credentialed live tests.

## Migration Plan

1. Inventory current hosting resources, caller URLs, Azure DevOps identity grants, shared dependencies, and deployment settings. Preserve the previous workflow/artifact reference and define an operator-owned rollback window; default to seven days after successful caller cutover.
2. Prepare the single-tenant API registration and calling identities, permissions/consent, and repository tenant/client/audience values. Confirm .NET 10/B1 availability and cost in the target region.
3. Implement and validate the combined host, routing, authorization, publish output, and local/test tooling, including clean-rebuilt dev-container bootstrap, isolated tests, and Cosmos trust, without changing the existing Azure deployment.
4. Provision the new plan/web app incrementally alongside legacy resources, preserving Cosmos and monitoring. Grant the new managed identity Azure DevOps access and deploy the combined artifact.
5. Run published-host checks and the deployed API/authentication/browser checks. Record successful results; do not retire anything on failure.
6. Update pipeline callers to the web app URL and audience-specific bearer tokens. Confirm their real compile round trips and public viewer navigation. Keep legacy resources and credentials available for the agreed rollback window.
7. Rollback during the window by restoring caller URLs/key usage and the previous deployment reference. Shared Cosmos data stays in place, so no database restore is needed. If a rollback requires moving traffic, keep both identities' outbound access until the decision is final.
8. After operator approval, delete only the inventoried legacy SWA/link, Function App, old FC1 plan, and Functions-only storage and associated roles. Revoke obsolete identity grants and secrets. Preserve Cosmos and shared monitoring; rerun new-host smoke checks. After retirement, rollback requires reprovisioning the old hosts rather than a URL-only switch.