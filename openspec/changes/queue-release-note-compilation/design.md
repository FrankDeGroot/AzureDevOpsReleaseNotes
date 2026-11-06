# Design

## Context

See [proposal.md](proposal.md) for motivation and scope decisions.

`ReleaseFunctions.Compile` currently validates and normalizes `CompileRequest`, then awaits `ReleaseNoteCompiler.CompileAsync` and responds with HTTP 200. The compiler already separates Azure DevOps retrieval from Table Storage persistence and generates `{projectId}:{buildId}`. `TableReleaseNoteStore` uses replace-upsert at that identity, which provides record-level idempotency, not exactly-once execution.

The API is a .NET 10 isolated Functions worker. It has HTTP bindings but no queue extension. Bicep already configures identity-based `AzureWebJobsStorage` queue access and assigns Storage Queue Data Contributor to the Function App. Azurite already provides local Blob, Queue, and Table storage. The client has no compile call.

`CompiledReleaseFixture` currently consumes the POST document immediately; its tests assume HTTP 200. The Azure DevOps setup script reuses a qualifying build, so merely polling the same release could pass even with a broken queue trigger. A fresh real build is required for a freshness-dependent live round trip. The pending App Service change conflicts with this Functions-specific design and must be reconciled separately.

## Goals / Non-Goals

**Goals:**

- Make acknowledgment dependent only on validation and durable queue submission, never on compilation.
- Reuse existing domain behavior and identity configuration with thin HTTP/queue adapters.
- Verify real trigger registration and transport behavior, not just direct worker invocation.

**Non-Goals:**

- No in-memory fire-and-forget tasks, custom polling daemon, Durable Functions, or Service Bus.
- No per-job persistence, freshness marker on release documents, ordering guarantee, or request deduplication ledger.
- No change to public reads, Function authorization, dependency health shape, or storage migration.

## Decisions

### 1. Submit through an awaited queue service

Add `Azure.Storage.Queues` and the isolated-worker Storage Queues extension using compatible supported package versions. Register a small injectable submission service around `QueueClient`; inject it into `ReleaseFunctions` instead of injecting the compiler into the HTTP handler. Keep existing metadata checks and the project-name fallback.

Use a versioned JSON envelope with `version: 1`, a newly generated `correlationId`, and the normalized request. Keep this transport model API-local; add only the typed acknowledgment to Shared. Serialize with explicit web JSON defaults. Configure both SDK producer and Functions queue extension for Base64 encoding, and measure the encoded message against the Storage Queue 64 KiB limit before sending. Reject oversized metadata with HTTP 400 and a descriptive JSON error before storage submission. Do not enqueue credentials or log complete bodies.

Await `SendMessageAsync` with the HTTP cancellation token. Set message TTL to non-expiring so worker downtime does not silently expire accepted work. Use `CreateIfNotExistsAsync` for local emulator initialization; Azure queues are provisioned declaratively. A recognized queue-service `RequestFailedException` at submission maps to a logged HTTP 503 JSON error. Configuration/programming errors remain explicit failures; do not broadly catch every exception. Cancellation does not produce an acknowledgment.

An explicit SDK send is preferred over an HTTP output binding because it exposes send failure before the success response is constructed. In-process background execution is rejected because it loses work on shutdown.

### 2. Return an availability acknowledgment, not a job

HTTP 202 body:

```json
{
  "id": "project-id:42",
  "projectId": "project-id",
  "releaseUrl": "/api/releases/project-id%3A42?projectId=project-id"
}
```

`Location` equals `releaseUrl`. Use an origin-relative URL under `/api` so the same value works through the SWA proxy and local Functions host without trusting forwarded host headers. Escape ID and project ID as path/query components. Do not include Function keys in URLs.

A successful GET reports an available release, not completion of this particular submission. Before first persistence, existing JSON 404 semantics remain. During recompilation, leave the previous release readable; do not delete it or add pending documents to the viewer.

There is an unavoidable ambiguous send outcome if storage accepts the message but the HTTP connection fails before acknowledgment. Callers can resubmit; duplicate processing is supported. This is not a transactional exactly-once submission protocol.

### 3. Let the Functions runtime own processing and retries

Add a `CompileReleaseQueued` Function with a queue trigger bound to `release-note-compilation` through `AzureWebJobsStorage`. Bind enough message metadata to log message ID and dequeue count. Deserialize and validate the envelope before calling the unchanged `ReleaseNoteCompiler.CompileAsync` with the invocation cancellation token.

Configure `host.json` queue settings explicitly: matching Base64 encoding, `maxDequeueCount: 5`, a 30-second failure visibility timeout, and a five-second maximum polling interval. Retain existing logging settings. Let the host handle visibility renewal, successful deletion, retry, and movement to `release-note-compilation-poison`; do not implement competing acknowledgment logic. These timings control ordinary retries, not a strict crash-recovery deadline.

Log start and persistence success with correlation/release/message IDs. On processing failure, log and rethrow; cancellation must also propagate rather than mark completion. Invalid JSON, unsupported envelope versions, and invalid metadata are explicit processing failures. Failed attempts leave an existing release unchanged.

Do not consume or auto-delete poison messages. Operators inspect their correlation IDs, repair the underlying issue, and explicitly replay selected messages through the submission path or queue tooling. Logs plus retained poison messages satisfy the selected failure contract without a job-status table.

### 4. Reuse storage and managed identity

Provision the work and poison queues under the existing storage account queue service in `infra/workload.bicep`; do not replace the account or table. Reuse the existing queue-contributor role assignment.

Use the same `AzureWebJobsStorage` connection family for producer and consumer. Locally use `AzureWebJobsStorage=UseDevelopmentStorage=true` (or its configured development connection string). In Azure construct the producer from `AzureWebJobsStorage:queueServiceUri` and the existing `TokenCredential`; keep the account-name configuration already required by the host. Missing settings must produce a clear startup/configuration failure, never an empty queue client or success fallback.

Keep the queue name fixed in production and allow a test-host-only override through the existing configuration mechanism so emulator lifecycle tests can use isolated queue names and matching poison names. Do not add a configurable production work-routing feature.

The deployment already has the required endpoints and RBAC; add only queue resources and any explicit identity setting required by the selected extension. Confirm generated Functions metadata discovers both HTTP and queue triggers when publishing.

### 5. Validate the actual asynchronous path

Add focused tests in `tests/Api.Tests` using the existing xUnit/Moq and stub-HTTP patterns:

- HTTP validation/normalization, exact HTTP 202 acknowledgment and Location, no inline Azure DevOps/store calls, and queue submission failure.
- Producer/consumer envelope and encoding round trip, encoded-size rejection, malformed/unsupported messages, and exception/cancellation propagation.
- Worker success and duplicate delivery using stubbed Azure DevOps HTTP responses and a test-owned store.

Use isolated Azurite queues and test-owned records for transport and poison-lifecycle checks. Run the real Functions host with those queue settings to verify trigger discovery, durable delivery after worker restart, and exactly five failing dequeues before poison retention. Stop only the test-owned host and delete only resolved test queues; never clear shared emulator data.

Update `CompiledReleaseFixture` to capture the acceptance response, verify its fields/header, then poll the existing GET using a monotonic 120-second deadline and one-second delay. Each HTTP read must honor the remaining overall deadline rather than inherit an independent 100-second timeout. Retry only 404; expose other HTTP/JSON failures immediately. Report the release ID and sanitized last response on timeout.

Preflight the live release GET. If it already exists, fail the freshness-dependent test with instructions to prepare a new actual build; do not silently count cached data as queue verification or synthesize a fake project ID. Add a focused `-NewBuild` option to the existing Azure DevOps setup script that bypasses qualifying-build reuse and reuses its existing new linked-commit/build path. Preserve idempotent reuse as the default. Refresh `.env` and repository build variables before a live local/deployed round trip; document this prerequisite explicitly for every deployment validation run. Do not give the API identity write permissions or have tests delete production releases.

Repeat-submission tests verify acceptance and one release ID; focused worker tests prove duplicate execution. The live first-compilation round trip proves the actual trigger path. Existing browser tests remain read-only and need no job UI changes.

## Risks / Trade-offs

- [Existing GET can be stale] -> Clearly document availability-only polling and use fresh real build IDs for lifecycle verification.
- [Freshness verification requires new test fixtures] -> Add opt-in fresh-build setup using existing script logic; operators refresh CI variables before live runs. A stale fixture fails clearly rather than giving false confidence.
- [Accepted work can fail permanently] -> Five attempts, explicit failure logs, retained poison messages, and bounded caller polling; no promise of per-job completion status.
- [Duplicate or concurrent execution can replace the same document] -> Preserve replace-upsert; no exactly-once or ordering claim.
- [Producer/trigger encoding mismatch prevents processing] -> Explicit matching Base64 configuration and a real host/emulator lifecycle check.
- [Unbounded backlog consumes storage and dependency quota] -> Document queue depth/oldest message/poison monitoring and controlled replay; retain work rather than silently expire it.
- [Rollback abandons queued work] -> Keep queue resources, inventory pending messages, and reconcile them explicitly; never purge them during rollback.
- [App Service migration removes the worker and storage prerequisites] -> Reconcile that proposal separately before either change is combined; do not apply its resource retirement plan to these queues.

## Migration Plan

1. Prepare and validate packages, HTTP/queue tests, generated trigger metadata, and isolated Azurite lifecycle behavior.
2. Update pipeline callers to accept HTTP 202 and optionally poll with a bounded timeout; no longer parse POST as a release document. Explain recompilation limitations.
3. Provision queues/RBAC incrementally before deploying the updated Functions package. Preserve the storage account and release table.
4. Prepare a fresh qualifying real test build with the opt-in setup path, refresh test variables, and run local and deployed acceptance/read-back checks. Confirm processing logs and empty poison queues for successful work.
5. During rollout monitor failed dequeues, poison messages, queue age, and dependency throttling.
6. To roll back, restore the prior synchronous package and caller expectations. Keep queues and messages intact. Inventory queued work and explicitly re-submit or drain it after resolving the rollback; already stored releases remain available. Do not automatically delete infrastructure or replay messages.
