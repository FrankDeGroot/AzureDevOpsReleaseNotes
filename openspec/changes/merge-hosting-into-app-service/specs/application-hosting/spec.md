# Spec Delta

## Purpose

Provide a single deployable web application that serves the release-notes viewer and API from one origin in Azure and during local development.

## ADDED Requirements

### Requirement: Combined application publish output
The application SHALL publish one runnable artifact containing the API host and the Blazor WebAssembly client assets. Deploying that artifact SHALL serve both components without a separate frontend deployment, API host, or reverse proxy process.

#### Scenario: Start the published application
- **WHEN** a maintainer publishes the server project in Release configuration and starts the resulting artifact
- **THEN** the root URL serves the release-notes client and `/api/releases` reaches the API on the same origin
- **AND** the client boot files and runtime assets load from that artifact

### Requirement: Client routing preserves API and asset errors
The host SHALL support direct navigation to client routes while preserving API status codes and missing-asset errors. Client API calls SHALL use the application's origin and SHALL NOT require cross-origin configuration.

#### Scenario: Refresh a release detail URL
- **WHEN** a user opens or refreshes an existing client release-detail route directly
- **THEN** the host serves the client entry document and the client loads that release through the same-origin API

#### Scenario: Unknown API endpoint
- **WHEN** a caller requests an unmapped `/api` path
- **THEN** the response is HTTP 404 rather than the client entry document

#### Scenario: Missing release or invalid request
- **WHEN** an API operation returns an error such as a missing release or malformed compile payload
- **THEN** the host preserves the API status and JSON error response instead of rewriting it to client HTML

#### Scenario: Missing static asset
- **WHEN** a caller requests a nonexistent client script, stylesheet, runtime asset, or configuration file
- **THEN** the response is HTTP 404 rather than client HTML with HTTP 200

### Requirement: One local application host
The repository SHALL provide a documented command that starts the combined application at `http://localhost:7071` in Development. The client and API SHALL be reachable without Static Web Apps CLI, Functions Core Tools, or a Functions storage emulator. Local Cosmos DB and developer Azure DevOps credentials SHALL remain supported.

#### Scenario: Run locally
- **WHEN** a developer configures the Cosmos emulator, signs in to Azure CLI, and runs the documented server command
- **THEN** the viewer is available at `http://localhost:7071` and the API at `http://localhost:7071/api`
- **AND** no separate client or proxy process is required

### Requirement: Rebuilt development container supports application workflows
After a clean rebuild and documented bootstrap, the dev container SHALL support restoring, building, publishing, and running the combined application and executing its unit and browser tests. Required SDKs, command-line tools, browser binaries, and Linux libraries SHALL be available without undocumented host-specific setup. The forwarded application port SHALL be 7071.

#### Scenario: First run after rebuild
- **WHEN** a developer rebuilds the container and follows the documented bootstrap using only committed configuration
- **THEN** restore, build, and Release publish succeed and the combined host is reachable through forwarded port 7071
- **AND** browser tests can launch Chromium without manually repairing missing tools, binaries, or Linux libraries

#### Scenario: Workspace is mounted from Windows
- **WHEN** the developer runs documented browser build and test commands on a Windows-backed workspace mount
- **THEN** generated executable browser-test output uses a writable Linux filesystem location and does not depend on unsupported mounted-filesystem permission changes

### Requirement: Local verification distinguishes isolated and live tests
The dev container SHALL support unit tests and explicitly selected stub-browser tests without Azure sign-in or live external services. Live dependency tests SHALL retain their documented Cosmos emulator, Azure CLI sign-in, and Azure DevOps test-project prerequisites. Container lifecycle hooks SHALL NOT require credentials or run live tests automatically.

#### Scenario: Credential-free verification
- **WHEN** a developer has not signed in to Azure and runs unit tests and explicitly selected stub-browser tests against the local host
- **THEN** those checks can pass without live Azure DevOps or Cosmos requests
- **AND** the host and browser test process do not require deployed Easy Auth configuration

#### Scenario: Run live dependency checks
- **WHEN** the developer has a ready Cosmos emulator, a signed-in Azure CLI identity, and valid Azure DevOps test-project settings
- **THEN** API round-trip and live browser checks run against the combined Development host
- **AND** missing prerequisites are reported as setup failures rather than silently switching to stub mode

### Requirement: Repeatable local emulator startup
The dev container SHALL start its Cosmos dependency with persistent data and establish readiness and HTTPS certificate trust before declaring emulator setup complete. Startup and trust operations SHALL be repeatable and report bounded, actionable failures. Removing Functions-only services SHALL NOT remove Cosmos data or require disabling TLS validation.

#### Scenario: Rebuild or restart
- **WHEN** the developer rebuilds or restarts the container without deleting its Cosmos data volume
- **THEN** existing emulator data remains available and startup reestablishes HTTPS trust for the current emulator certificate
- **AND** the application can reach Cosmos without globally bypassing certificate validation

#### Scenario: Emulator does not become ready
- **WHEN** Cosmos readiness or certificate setup fails within the documented startup limit
- **THEN** setup reports the failing dependency or trust step and the documentation identifies recovery steps
- **AND** no successful live-test result is reported