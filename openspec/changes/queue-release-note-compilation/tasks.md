# Tasks

## 1. Queue transport and provisioning

- [ ] 1.1 Add supported Azure Storage Queues SDK and isolated-worker queue extension references to `src/Api/Api.csproj`; verify restore and `dotnet build src/Api/Api.csproj`.
- [ ] 1.2 Implement the API-local versioned envelope and injectable submission service with explicit Base64 encoding, encoded-size validation, non-expiring TTL, credential-free payloads, and cancellation; verify focused producer round-trip, size-boundary, and send-failure tests in `tests/Api.Tests`.
- [ ] 1.3 Register the queue client/submission service in `src/Api/Program.cs` using the existing `AzureWebJobsStorage` connection or queue URI plus `TokenCredential`; verify local/identity configuration tests and actionable missing-configuration errors.
- [ ] 1.4 Provision work and poison queues in `infra/workload.bicep` using the existing account and queue RBAC; verify `az bicep build --file infra/main.bicep` and inspect a deployment preview for preservation of the account/table.
- [ ] 1.5 Update `docs/environment-configuration.md`, `docs/local-emulators.md`, and `docs/deployment.md` with queue names, existing identity/connection settings, and message retention; verify the documented local configuration supports the producer and trigger without adding cloud keys.

## 2. HTTP acceptance contract

- [ ] 2.1 Add the typed `id`, `projectId`, and `releaseUrl` acknowledgment in Shared and change `ReleaseFunctions.Compile` to normalize and await queue submission before returning HTTP 202 and matching origin-relative Location; verify exact response/header serialization and escaping tests.
- [ ] 2.2 Preserve malformed/missing metadata HTTP 400 and Function authorization; map oversized transport input to descriptive HTTP 400 and recognized storage submission failure to logged JSON HTTP 503; verify failure cases enqueue nothing or return no acknowledgment and make no inline compiler/store calls.
- [ ] 2.3 Update `README.md`, `docs/architecture.md`, and `docs/run-the-app.md` with an actual submission/polling example, acceptance-versus-completion semantics, and stale-read behavior during recompilation; verify the example's URL and JSON fields match endpoint tests.

## 3. Background processing and operational failures

- [ ] 3.1 Add the isolated queue-triggered Function, validate envelopes/metadata, and invoke the existing compiler with the invocation cancellation token; verify worker success, unsupported/malformed messages, propagated failures/cancellation, and duplicate upsert tests.
- [ ] 3.2 Configure `host.json` for matching Base64 encoding, five total attempts, 30-second failure visibility, and five-second maximum polling; verify a published API contains both compile HTTP and queue-trigger metadata and preserves existing logging settings.
- [ ] 3.3 Add correlated submission/start/success/failure logs with release/message IDs and dequeue count without credentials or complete bodies; verify captured logs for successful and failing worker tests.
- [ ] 3.4 Document queue depth/age, poison inspection, controlled replay, and rollback backlog reconciliation in `docs/architecture.md` and `docs/deployment.md`; verify the procedures retain messages and do not introduce automatic poison consumption or resource deletion.

## 4. Asynchronous integration fixtures

- [ ] 4.1 Adapt `CompiledReleaseFixture` and `ReleaseApiIntegrationTests` to assert HTTP 202 and Location, preflight for an absent release, then poll GET with a 120-second overall deadline and one-second delays; verify focused polling tests cover delayed availability, deadline enforcement, non-404/JSON failures, and cached-record rejection.
- [ ] 4.2 Preserve read-with/without-project-ID, project listing, repeat submission, and Table Storage/Azure DevOps health assertions; verify they operate on the polled document, while duplicate worker execution is proven by worker tests rather than cached GET.
- [ ] 4.3 Add an opt-in `-NewBuild` switch to `scripts/Initialize-AzureDevOpsTestProject.ps1` that bypasses build reuse and reuses its existing linked-commit/build creation logic; verify default reuse remains unchanged and a requested fresh build has commits/work items and updates the configured build ID.
- [ ] 4.4 Update `docs/integration-tests.md`, `docs/set-up-the-azure-devops-test-project.md`, and `docs/deploy-with-github-actions.md` with fresh-build preparation, `.env`/repository-variable refresh before each live run, polling diagnostics, and the unchanged Function-key flow; verify documented prerequisites produce an initially absent release without deleting existing records.
- [ ] 4.5 Add isolated Azurite transport/lifecycle tests using resolved test-owned queue names and records; verify real Functions-trigger execution, persistence after delayed worker startup, and poison retention after five failing attempts, with cleanup limited to the test host and test queues.

## 5. End-to-end verification

- [ ] 5.1 Run the API unit tests and build/publish after all changes; verify compilation, generated queue trigger registration, and unchanged HTTP read/error contracts, without starting live tests in the unit-only run.
- [ ] 5.2 Run the local real-host integration round trip against Azurite and a fresh qualifying Azure DevOps build; verify HTTP 202 followed by an initially absent release becoming available, expected commits/work items, and one record after repeated submission.
- [ ] 5.3 Prepare a separate fresh deployed test build, deploy queues then the updated Functions package, and run the existing deployed integration workflow; verify actual background persistence, masked Function credentials, no poison message for successful work, and unchanged viewer reads.

## Workflow follow-up

- Review these artifacts before starting implementation.
- Reconcile `merge-hosting-into-app-service` separately before combining the hosting changes or retiring the Functions worker/storage infrastructure.
