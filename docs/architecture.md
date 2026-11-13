# Architecture

## Software Components

- **Frontend**: Blazor WebAssembly ([src/Client](../src/Client))
- **Backend**: Azure Functions isolated worker ([src/Api](../src/Api))
- **Shared**: Domain models ([src/Shared](../src/Shared))
- **Compilation queues**: Azure Storage Queues carries durable compile requests to a background Functions trigger.

## Infrastructure Components

- **Static Web App**: Hosts the Blazor WebAssembly frontend.
- **Azure Functions**: Hosts the backend API using a system-assigned managed identity.
- **Azure Storage Tables**: Stores compiled release notes in the existing Functions StorageV2 account; the Function identity has table data access.
- **GitHub Actions**: Automates the deployment of the application components.
- **Bicep**: Infrastructure as code for provisioning Azure resources.

## Compilation flow

The caller posts build metadata to `POST /api/releases/compile`. The API validates and normalizes it, submits a credential-free message to `release-note-compilation`, then returns HTTP 202 with `id`, `projectId`, and an origin-relative `releaseUrl` (also sent as `Location`). The queue-triggered Function retrieves Azure DevOps data and upserts the release in Table Storage; successful processing removes the message. Failed messages are retried and eventually retained in `release-note-compilation-poison`.

HTTP 202 confirms queue acceptance only, not completion or guaranteed success. Callers poll the existing GET endpoint with a bounded timeout and retry only 404. Until the first persistence, GET returns 404. If a release already exists during recompilation, it remains readable; that prior result may be stale and does not indicate that the latest submission completed. No per-job status endpoint is provided.

## Queue operations

Monitor the approximate message count and the age of the oldest visible work message. Queue message insertion time provides the age check; inspect messages with a peek operation so monitoring does not dequeue or hide work. Review `release-note-compilation-poison` separately. Worker logs correlate the release ID and correlation ID with the queue message ID and dequeue count.

After correcting a failure, replay only selected poison messages: verify their metadata and correlation ID, submit the same build through the authorized compile endpoint or requeue the inspected envelope with approved queue tooling, and retain the poison copy until persistence is confirmed. Do not automatically consume poison messages or purge a queue as part of routine operations.
