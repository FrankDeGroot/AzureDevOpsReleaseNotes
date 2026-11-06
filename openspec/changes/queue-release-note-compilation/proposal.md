# Proposal

## Why

Compilation currently holds the pipeline HTTP request open while querying Azure DevOps and writing release notes. Durable queue submission lets the request finish promptly while the existing Azure Functions app compiles independently of the caller's connection.

## What Changes

- **BREAKING**: Keep `POST /api/releases/compile`, its Function authorization, and metadata validation, but return HTTP 202 only after successfully submitting a Storage Queue message instead of HTTP 200 with a compiled document.
- Return an acknowledgment containing `id`, `projectId`, and `releaseUrl`, with a matching `Location` header. Callers can poll the existing release GET endpoint.
- Add a queue-triggered Function that reuses `ReleaseNoteCompiler` and the current Table Storage upsert behavior, with bounded retries, poison-message handling, and correlated logs.
- Provision the work and poison queues in the existing storage account; use managed identity in Azure and Azurite locally.
- Update compile integration tests and caller documentation for acceptance followed by eventual availability.
- Explicitly document that an existing release remains readable during recompilation; a successful GET is not proof that the latest submission completed.

## Capabilities

### New Capabilities

- `release-note-compilation-queue`: Durable queue transport, background processing, delivery reliability, and operational configuration.

### Modified Capabilities

- `release-notes-compiler`: Replace synchronous compilation responses with validated queue submission and a release-location acknowledgment.
- `api-integration-tests`: Verify HTTP 202 and eventual release availability rather than deserializing the POST response as a release document.

## Impact

Affected areas include `src/Api/ReleaseFunctions.cs`, API dependency registration and package references, `host.json`, queue services/trigger adapters, shared acknowledgment models, `infra/workload.bicep`, API tests, and compile/configuration/operations documentation. The Blazor client only reads releases and needs no new job-status UI. Existing release document IDs, Table Storage schema, and Azure DevOps outbound identity remain unchanged.

The main specs still mention Cosmos DB, although the current code and completed `migrate-cosmos-db-to-table-storage` change use Table Storage. This proposal follows the current implementation and does not migrate data or revise unrelated storage requirements.

User-selected scope is the current Azure Functions host. The pending `merge-hosting-into-app-service` proposal assumes no non-HTTP triggers, preserves HTTP 200, and removes Functions infrastructure. It must be reconciled separately before being applied alongside this change; this proposal does not edit that change or authorize a hosting migration.

No persisted job-status endpoint, exactly-once execution, freshness tracking, automatic poison replay, queue health API expansion, or hosting/authentication migration is included.
