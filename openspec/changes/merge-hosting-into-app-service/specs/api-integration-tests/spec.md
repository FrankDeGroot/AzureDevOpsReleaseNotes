# Spec Delta

## MODIFIED Requirements

### Requirement: Integration tests target a running API by base URL
The integration test suite SHALL send HTTP requests to a running API using a configurable base URL. Without a configured URL, it SHALL use the combined local host at `http://localhost:7071/api`. When a bearer token is configured, authenticated requests SHALL send it in the Authorization header. The suite SHALL NOT use Function keys or put credentials in URLs.

#### Scenario: Default local target
- **WHEN** the suite runs without a configured base URL or token
- **THEN** requests target `http://localhost:7071/api` and local Development compilation runs anonymously

#### Scenario: Deployed target with function key
- **WHEN** a caller targets the deployed compile endpoint with only a legacy Function key
- **THEN** the request fails with HTTP 401 and the suite does not treat the key as a supported credential

#### Scenario: Deployed target with bearer token
- **WHEN** the suite runs with the deployed App Service API URL and an access token
- **THEN** compilation targets that URL with `Authorization: Bearer <token>`
- **AND** credentials are not included in query strings or test output

### Requirement: Local integration run
The suite SHALL run against the combined local Development host using the dev-container Cosmos DB emulator and real Azure DevOps service. Outbound Azure DevOps calls SHALL use the developer's Azure CLI sign-in. No Azure-hosted Cosmos DB, App Service, Easy Auth, or legacy Functions/SWA host SHALL be required for local integration tests.

#### Scenario: Run locally
- **WHEN** a developer starts the combined host in Development, signs in with `az login`, loads `.env.ps1`, and runs integration tests
- **THEN** tests pass using the Cosmos emulator and Azure DevOps test project without an inbound token

### Requirement: Deployed integration run in GitHub Actions
GitHub Actions SHALL exclude integration tests before deployment and run them against the App Service `/api` URL after combined deployment. The workflow SHALL obtain a short-lived Entra token for the configured API audience using its authorized federated identity and mask it in logs. Any integration failure SHALL fail the workflow.

#### Scenario: Pre-deploy tests
- **WHEN** the workflow runs its pre-deploy test step
- **THEN** only unit tests execute

#### Scenario: Post-deploy integration tests
- **WHEN** the combined application deployment succeeds
- **THEN** the workflow runs the integration suite against the web app `/api` URL using a masked bearer token and repository Azure DevOps test settings
- **AND** compilation, read-back by ID, project listing, idempotency, and dependency health checks retain their existing assertions

#### Scenario: Integration failure fails the workflow
- **WHEN** an integration test fails against the deployed API
- **THEN** the workflow fails

## ADDED Requirements

### Requirement: Compilation authorization regression checks
Tests SHALL verify production compilation rejects anonymous, legacy-key-only, and invalid-token calls without side effects while public routes remain accessible. Tests SHALL verify anonymous local Development compilation and rejection outside that bypass. Authentication tests SHALL NOT require live Azure DevOps or Cosmos access just to assert rejection.

#### Scenario: Anonymous deployed compile request
- **WHEN** a deployed authorization check posts without authentication
- **THEN** it observes HTTP 401 with no sign-in redirect

#### Scenario: Invalid authentication at the platform boundary
- **WHEN** a deployed authorization check supplies an expired token or a token for the wrong API audience or tenant
- **THEN** the request is rejected before compilation

#### Scenario: Public deployed reads
- **WHEN** tests request the viewer, list, detail, and health routes without tokens
- **THEN** authentication does not block them and their existing application status semantics remain intact

#### Scenario: Local bypass boundaries
- **WHEN** focused host tests exercise local Development, non-Development, missing authentication configuration, and an Azure-hosted Development configuration
- **THEN** only the local Development case permits anonymous compilation
- **AND** spoofed platform headers and legacy keys do not authenticate the rejected cases