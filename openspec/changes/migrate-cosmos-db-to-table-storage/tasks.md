# Tasks

## 1. API Storage

- [x] 1.1 Replace the Cosmos SDK and store registration with an Azure Tables-backed `IReleaseNoteStore`, including entity mapping and local/hosted client configuration; verify `dotnet build src/Api` succeeds.
- [x] 1.2 Add storage unit tests for deterministic upsert, ID lookup with and without a project ID, nested document mapping, project filtering, global listing, and descending build-date order; verify the focused `Api.Tests` storage tests pass.
- [x] 1.3 Replace the Cosmos external health check with a Table health check and update its tests; verify `dotnet test tests/Api.Tests` passes.

## 2. Azure Provisioning

- [x] 2.1 Remove Cosmos resources and app settings from Bicep, reuse the existing Functions StorageV2 account and table-data role assignment, and add the release table plus Function App Table endpoint/name settings; verify `az bicep build --file infra/main.bicep` succeeds.
- [x] 2.2 Update deployment and environment configuration documentation for Function App managed-identity access, new settings, the empty-table cutover, and rollback while retaining existing Cosmos resources; verify documented resource names and settings match the Bicep outputs.

## 3. Local Development and Integration

- [x] 3.1 Configure Azurite Table Storage for the dev container and local API settings, removing Cosmos-emulator startup and certificate-trust dependencies; verify the API can connect to the local table service.
- [x] 3.2 Update integration-test setup and assertions for Table-backed compile, ID read, project list, idempotent upsert, and health; verify the integration tests pass against Azurite and the configured Azure DevOps test project.
- [x] 3.3 Update local-emulator and integration-test documentation for Azurite setup, clean-start behavior, and test prerequisites; verify every documented local command is valid in the dev container.

## 4. End-to-End Verification

- [x] 4.1 Run the full API unit-test suite, the configured integration tests, and Bicep compilation together; verify storage behavior, health reporting, and infrastructure validation all pass before cutover.