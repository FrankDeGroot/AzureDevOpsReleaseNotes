# Spec Delta

## Purpose

Verifies end to end that the release-notes Functions API reaches its real external dependencies: Cosmos DB and the Azure DevOps REST API. The checks run both against a local host and against the deployed Azure Function App.

## ADDED Requirements

### Requirement: Integration tests target a running API by base URL
The integration test suite SHALL send HTTP requests to a running API. The API base URL comes from configuration. When no base URL is configured, the suite SHALL use the local Functions host at `http://localhost:7071/api`. When a function key is configured, the suite SHALL send it in the `x-functions-key` header.

#### Scenario: Default local target
- **WHEN** the suite runs without a configured base URL
- **THEN** every request targets `http://localhost:7071/api`

#### Scenario: Deployed target with function key
- **WHEN** the suite runs with a configured deployed base URL and function key
- **THEN** every request targets that base URL and includes the key in the `x-functions-key` header

### Requirement: Integration tests are isolated from unit tests
Integration tests SHALL be categorized so that a filtered `dotnet test` run can exclude them, and so that they can be run on their own.

#### Scenario: Unit-only run
- **WHEN** `dotnet test` runs with the integration category excluded
- **THEN** no integration test executes and no external service is contacted

#### Scenario: Integration-only run
- **WHEN** `dotnet test` runs filtered to the integration category
- **THEN** only the integration tests execute

### Requirement: Missing Azure DevOps test configuration fails clearly
When the Azure DevOps organization, project, project ID, repository ID, or build ID is missing, the suite SHALL fail. The failure message SHALL name each missing setting and point to the setup script. The suite SHALL NOT report success.

#### Scenario: Build ID not configured
- **WHEN** the suite runs without a configured Azure DevOps build ID
- **THEN** the tests fail with a message naming the missing setting and the setup script

### Requirement: Health check verifies both dependencies
The suite SHALL verify that the health endpoint returns HTTP 200 with overall status `healthy`. The response SHALL report both the Cosmos DB check and the Azure DevOps check as healthy.

#### Scenario: Both dependencies reachable
- **WHEN** the suite calls `GET health` on a correctly configured API
- **THEN** the response is HTTP 200, overall status is `healthy`, and both the Cosmos DB and Azure DevOps checks report healthy

### Requirement: Compile round-trip through Azure DevOps and Cosmos DB
The suite SHALL compile release notes for the configured test build. It SHALL then read the stored record back from Cosmos DB through the API, by ID and in the project list. The test build has at least one commit and one linked work item.

#### Scenario: Compile a known build
- **WHEN** the suite posts the configured organization, project, project ID, repository ID, and build ID to `POST releases/compile`
- **THEN** the response is HTTP 200 with ID `{projectId}:{buildId}`, a non-empty build number, at least one commit, and at least one work item

#### Scenario: Read back by ID
- **WHEN** the suite calls `GET releases/{id}?projectId={projectId}` after a successful compile
- **THEN** the response is HTTP 200 and contains the same build ID, commits, and work items

#### Scenario: Listed for project
- **WHEN** the suite calls `GET releases?project={project}` after a successful compile
- **THEN** the response contains a record with the compiled ID

#### Scenario: Repeated compile is idempotent
- **WHEN** the suite compiles the same build twice
- **THEN** both calls succeed and the project list contains exactly one record with that ID

### Requirement: Local integration run
The suite SHALL run against a locally hosted API that uses the dev-container Cosmos DB emulator and the real Azure DevOps service. Azure DevOps is authenticated with the developer's Azure CLI sign-in. No Azure-hosted Cosmos DB or Function App SHALL be required.

#### Scenario: Run locally
- **WHEN** a developer starts the local Functions host, signs in with `az login`, loads `.env.ps1`, and runs the integration tests
- **THEN** the tests pass using the Cosmos DB emulator and the Azure DevOps test project

### Requirement: Deployed integration run in GitHub Actions
The GitHub Actions workflow SHALL exclude integration tests from its pre-deploy test step. After deploying the Function App, it SHALL run the suite against the deployed Function App URL, using a function key retrieved at runtime and masked in logs. The workflow SHALL fail if any integration test fails.

#### Scenario: Pre-deploy tests
- **WHEN** the workflow runs its pre-deploy test step
- **THEN** only unit tests execute

#### Scenario: Post-deploy integration tests
- **WHEN** the Function App deployment step succeeds
- **THEN** the workflow runs the integration suite against the deployed Function App URL with a masked function key and the Azure DevOps test settings from repository variables

#### Scenario: Integration failure fails the workflow
- **WHEN** any integration test fails against the deployed API
- **THEN** the workflow run fails
