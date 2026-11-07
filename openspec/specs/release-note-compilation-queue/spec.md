# release-note-compilation-queue Specification

## Purpose

Provide durable submission and background execution of release-note compilation, with bounded retries and actionable failure diagnostics.

## Requirements

### Requirement: Durable compilation delivery
Accepted compilation requests SHALL be stored in Azure Storage Queue before HTTP acknowledgment. Messages SHALL contain normalized build metadata and a correlation ID, but no caller credentials. Submitted messages SHALL remain available after the HTTP connection or API process ends.

#### Scenario: Worker starts after submission
- **WHEN** a request is successfully queued while no worker is processing messages
- **THEN** the worker can process that request after starting or restarting without requiring the caller to resubmit

#### Scenario: Credential separation
- **WHEN** an authenticated caller submits compilation metadata
- **THEN** the queue message contains no Function key or access token
- **AND** background Azure DevOps access uses the app's existing outbound identity

### Requirement: Background compilation preserves release behavior
The worker SHALL fetch Azure DevOps build data, commits, and linked work items and upsert the existing release document in Table Storage. Successful processing SHALL remove the queue message only after persistence completes. Duplicate delivery SHALL NOT create duplicate release records.

#### Scenario: Successful queued compilation
- **WHEN** the worker receives a valid compilation message and its dependencies are available
- **THEN** it persists the release under `{projectId}:{buildId}` with the existing document shape
- **AND** existing release read and list endpoints expose it

#### Scenario: Duplicate message delivery
- **WHEN** the same request is processed more than once
- **THEN** the project list contains exactly one release with that ID
- **AND** processing can repeat dependency queries and replace that document

### Requirement: Bounded retries and poison isolation
Failed processing SHALL be retried up to five total dequeue attempts. After exhaustion the message SHALL be moved to `release-note-compilation-poison`. Invalid message bodies SHALL be treated as failures, not successful no-ops. Failure handling SHALL NOT delete or replace an existing release with an error-shaped document.

#### Scenario: Dependency recovers
- **WHEN** compilation fails transiently and succeeds on a later attempt within the retry budget
- **THEN** the release is stored and the work message is removed

#### Scenario: Retry budget exhausted
- **WHEN** a message fails all five processing attempts
- **THEN** it is retained in the poison queue for operator inspection
- **AND** any previously stored release remains readable

#### Scenario: Invalid queued payload
- **WHEN** a message contains malformed JSON, an unsupported envelope version, or missing required metadata
- **THEN** processing fails with a diagnostic and follows the same retry and poison policy

### Requirement: Correlated operational diagnostics
The system SHALL log submission, processing start, successful persistence, and failed processing with a correlation ID and release ID where available. Worker diagnostics SHALL identify the queue message and dequeue attempt. Credentials and complete request bodies SHALL NOT be logged.

#### Scenario: Investigate failed compilation
- **WHEN** an operator investigates a poison message
- **THEN** its correlation ID can be matched to the submission and processing logs
- **AND** the error explains the processing failure without exposing credentials

### Requirement: Repeatable queue provisioning and configuration
Deployment SHALL provision `release-note-compilation` and its poison queue in the existing storage account. Azure queue access SHALL use managed identity without account keys. Local development SHALL support the existing Azurite Queue endpoint. Missing required queue configuration SHALL fail explicitly.

#### Scenario: Deploy or redeploy
- **WHEN** the workload infrastructure is applied
- **THEN** both queues exist and the Function App identity can send, receive, and delete messages
- **AND** existing queued messages and release tables are preserved

#### Scenario: Local processing
- **WHEN** the local Functions host runs with the documented Azurite configuration
- **THEN** submission and background processing use the Azurite Queue endpoint

#### Scenario: Missing Azure queue endpoint
- **WHEN** no local connection string or Azure queue endpoint is configured
- **THEN** queue initialization fails with an actionable configuration error rather than acknowledging compilation