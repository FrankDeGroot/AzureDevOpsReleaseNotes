# Proposal

## Why

The application currently needs separate Static Web App and Flex Consumption Function App deployments, a linked backend, and local proxy tooling to expose one user experience. Hosting the existing Blazor WebAssembly client and API in one ASP.NET Core App Service simplifies publishing, routing, and operations while retaining the current release-notes behavior and Cosmos DB data.

## What Changes

- Convert the .NET 10 Functions HTTP host in `src/Api` to ASP.NET Core and serve the existing Blazor WebAssembly client and `/api` endpoints from one origin and one publish artifact.
- Preserve release compilation, list/detail contracts, dependency health behavior, managed-identity Azure DevOps access, Cosmos DB storage, and Application Insights telemetry.
- **BREAKING** Replace Function-key compilation access with Microsoft Entra ID through App Service Easy Auth. Keep the viewer, read APIs, and health endpoint anonymous; require an authenticated identity for compilation in Azure. Permit anonymous compilation only in local Development.
- **BREAKING** Replace SWA/Functions infrastructure outputs, deployment actions, and URLs with one Linux App Service web app and a supported dedicated plan. Update CI callers to use bearer tokens rather than Function keys.
- Run the combined app locally without SWA CLI, Functions Core Tools, or Functions-only storage dependencies; update test configuration, environment setup, and documentation accordingly.
- Update the dev container to restore, build, publish, run, and test the combined app after a clean rebuild, including .NET/PowerShell tooling, Playwright Chromium and Linux dependencies, Cosmos readiness and certificate trust, and port 7071. Support credential-free unit and stub-browser tests, with developer sign-in and test-project settings required only for live Azure DevOps checks.
- Cut over only after verification, retain a documented rollback window, and explicitly retire the old SWA, Function App, and exclusively owned hosting/storage resources afterward without deleting Cosmos data.

## Capabilities

### New Capabilities

- `application-hosting`: Single-origin ASP.NET Core hosting, combined publishing, client routing, and local execution.

### Modified Capabilities

- `azure-workload-deployment`: Provision and deploy the combined App Service with Easy Auth, preserve dependency configuration, and document verified cutover and retirement.
- `release-notes-compiler`: Require Easy Auth identity for Azure compilation while allowing anonymous local Development calls.
- `api-integration-tests`: Target the combined host, use bearer tokens in Azure, and verify production authorization and local bypass behavior.
- `environment-configuration`: Replace Function-key setup with token and Easy Auth configuration and document the new local/deployed workflow.

## Impact

- Application: `src/Api/Api.csproj`, host startup and HTTP entry points, `src/Client` publish integration, routing/configuration, and server telemetry. Shared models and domain services remain intact.
- Infrastructure: `infra/main.bicep`, `infra/workload.bicep`, parameters, managed identity, Easy Auth settings, deployment outputs, and retirement instructions. Existing Cosmos DB resources and records must be preserved.
- Automation: `.github/workflows/azure-static-web-apps.yml`, API and browser tests, `.env.example.ps1`, environment sync, and the Azure DevOps setup script's Function-specific identity lookup.
- Local experience: `.devcontainer/devcontainer.json`, `.devcontainer/compose.yaml`, certificate-trust/bootstrap hooks, and generated feature lock metadata when feature declarations change; package dependencies, launch settings, and run/setup/deployment/test documentation. Container setup must support Linux-local Playwright output for Windows-backed mounts and repeatable rebuild/start behavior without embedding credentials or running live tests automatically.
- Compatibility/cost: New hostname and Entra token requirements affect pipeline callers. A dedicated Linux plan introduces a standing compute charge; use the lowest supported tier suitable for .NET 10 and Easy Auth rather than assuming consumption pricing.
- Existing deployment and integration specs mandate SWA/Functions and Function keys. This change intentionally supersedes those requirements through explicit deltas; no implementation or Azure resource changes occur during proposal creation.