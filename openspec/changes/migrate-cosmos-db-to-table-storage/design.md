# Design

## Context

See proposal.md for the motivation. The API is an Azure Functions isolated-worker app and already depends on `IReleaseNoteStore`, but its only implementation uses Cosmos DB. `infra/workload.bicep` provisions a StorageV2 account for the Functions host, exposes its Table service endpoint, and assigns the Function App's identity the Storage Table Data Contributor role. Release IDs are formed from `ProjectId` and `BuildId`; project listing accepts a project name and returns releases ordered by build date. Release documents also contain nested commit and work-item collections.

## Goals / Non-Goals

**Goals:**

- Replace Cosmos-specific persistence with Azure Storage Tables without changing API routes or response documents.
- Use Microsoft Entra authentication for the hosted app and Azurite for local table development.
- Keep deterministic upserts and support project-filtered, global, and ID-based reads.

**Non-Goals:**

- Copy or dual-read existing Cosmos records.
- Change the release-note document schema or API contract.
- Delete existing Cosmos resources as part of the application deployment.

## Decisions

### Azure.Data.Tables with environment-specific credentials

Use the Azure Tables SDK. In Azure, construct the table client from the configured Table service URI and the Function App's existing managed-identity credential. Reuse the Functions host StorageV2 account and its existing Storage Table Data Contributor assignment; do not add another account or role assignment, and do not put storage account keys in hosted app settings. For local development, use an Azurite connection string because Azurite does not provide the hosted Entra authentication path.

Configure the table endpoint and table name as Function App settings, and keep the Azurite connection string in local-only settings. Provision the release table as a child resource of the existing account and have application startup ensure it exists idempotently.

### Stable entity keys and document mapping

Use `ProjectId` as `PartitionKey` and the invariant-culture `BuildId` as `RowKey`. Preserve the existing `Id` string (`ProjectId:BuildId`) as an entity property and reconstruct the same `ReleaseNoteDocument` shape when reading. Use the partition and row keys for point reads when a project ID is available; for ID-only reads, derive both keys from the existing ID format, splitting at the final colon.

Map scalar document fields to supported Table properties. Serialize `Commits` and `WorkItems` into separate JSON string properties and deserialize them on reads. Validate payloads against Azure Table entity and property size limits and return a clear storage failure rather than silently truncating data.

### Preserve query behavior with paged Table queries

Use a partition query when the store can address a project ID directly. The current list API accepts a project name, not a project ID, so a project-filtered list will query by the `Project` property across partitions. An unfiltered list will also scan the table. Consume all pages, map the entities, filter as needed, and sort by `BuildDate` descending in the application because Table Storage cannot globally order cross-partition results by that property.

This favors compatibility with the current API over adding a new project-ID query parameter or maintaining a separate index table. The existing list already materializes all results, but Table queries make the cross-partition cost and latency more explicit.

### Provision a new table without a data copy

Reuse the existing Functions host StorageV2 account in Bicep, provision the release table, and configure the Function App's table endpoint/name settings. Retain the existing table-data role assignment. Add Azurite's Table endpoint to the local container environment and replace Cosmos-only emulator guidance and checks.

The new table starts empty. Leave any already-provisioned Cosmos account untouched during this change so it remains available for rollback or manual retention; do not implement a record-copy or dual-write path.

## Risks / Trade-offs

- **Cross-partition reads can become slower or more expensive as history grows** → Keep API behavior unchanged for this migration, page through results, and sort only after retrieval. Revisit an index or API change if observed data volume makes this unacceptable.
- **Nested collections or documents may exceed Table property/entity limits** → Serialize nested collections separately, validate limits, and cover representative documents in tests.
- **A clean start hides old Cosmos records, and rollback hides new Table writes from the old version** → Keep Cosmos resources and records untouched; document that rollback restores old reads but does not merge writes made after cutover.
- **Function App settings may use a Table endpoint that does not match the provisioned account** → Derive the setting from the storage account's `table` primary endpoint and verify the Table health check after deployment.

## Migration Plan

1. Deploy the release table and Function App settings using the existing StorageV2 account and managed-identity role assignment. Keep the existing Cosmos resource intact; removing its declaration from an incremental deployment does not delete it.
2. Deploy the Table-backed application and verify the Table health check before enabling normal use.
3. Begin storing new releases in the empty Table; do not copy existing Cosmos records.
4. To roll back, restore the previous application version and Cosmos settings. Records written to Table after cutover remain in Table and are not visible to the Cosmos-backed version.