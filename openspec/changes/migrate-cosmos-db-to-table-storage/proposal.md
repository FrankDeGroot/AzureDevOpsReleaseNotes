# Proposal

## Why

Replace the release-notes persistence dependency on Cosmos DB with Azure Storage Tables. This aligns the application and its local development environment with the requested storage service while reducing the number of Cosmos-specific resources and operational steps.

## What Changes

- Replace Cosmos DB persistence with Azure Storage Tables while retaining the existing release-note API behavior.
- Reuse the Function App's existing StorageV2 account and table-data role assignment, and configure a release table for the Function App's system-assigned identity.
- Update local development, health checks, and tests to use Azurite Table Storage.
- **BREAKING** Remove Cosmos-specific application settings and infrastructure. New deployments start with an empty table; existing Cosmos records will not be copied and will no longer be available through the application. Existing Cosmos resources remain untouched by the incremental deployment.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `release-notes-storage`: Change the persistence service while retaining deterministic release identity, upsert behavior, lookup, and chronologically ordered project listings.

## Impact

- API storage implementation and dependency injection in `src/Api`.
- Azure resource provisioning and app settings in `infra`.
- Local emulator configuration and setup documentation in `.devcontainer` and `docs`.
- Storage, health, and integration tests in `tests`.
- No HTTP route or release-note response-shape changes are intended. Existing Cosmos records are out of scope for migration.