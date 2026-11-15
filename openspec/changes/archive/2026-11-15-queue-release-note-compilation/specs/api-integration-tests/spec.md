# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: Compile round-trip through Azure DevOps and Cosmos DB`
- TO: `### Requirement: Asynchronous compile round-trip through Azure DevOps and release storage`

## MODIFIED Requirements

### Requirement: Asynchronous compile round-trip through Azure DevOps and release storage
The suite SHALL submit a configured real Azure DevOps build, verify HTTP 202 and the acknowledgment, and poll until its release is available in Table Storage through the API. It SHALL verify read-by-ID and project listing. Polling SHALL stop after 120 seconds, retry only HTTP 404, and fail with actionable diagnostics on timeout or other errors. Worker execution SHALL NOT be inferred from a pre-existing record.

#### Scenario: Compile a known build
- **WHEN** the suite posts the configured organization, project, project ID, repository ID, and build ID to `POST releases/compile`
- **THEN** the response is HTTP 202 with ID `{projectId}:{buildId}`, project ID, a release URL, and a matching `Location` header
- **AND** the acknowledgment is not deserialized as a compiled release document

#### Scenario: Read back by ID
- **WHEN** the suite polls an initially absent release after accepted compilation
- **THEN** it eventually receives HTTP 200 with the configured build ID, a non-empty build number, at least one commit, and at least one linked work item
- **AND** reads with and without project ID return the same document

#### Scenario: Listed for project
- **WHEN** the suite calls `GET releases?project={project}` after background persistence
- **THEN** the response contains a record with the compiled ID

#### Scenario: Repeated compile is idempotent
- **WHEN** the suite submits the same build twice
- **THEN** both submissions return HTTP 202 and the project list contains exactly one record with that ID
- **AND** duplicate processing is verified by focused worker tests rather than by treating an existing GET response as evidence of the second worker execution

#### Scenario: Polling expires
- **WHEN** an initially absent release remains unavailable for 120 seconds
- **THEN** the test fails with the release ID, last response, and directions to inspect processing logs and the poison queue

#### Scenario: Non-retryable read failure
- **WHEN** polling receives an authentication error, server error, or invalid release JSON
- **THEN** the test fails immediately rather than hiding the error behind retries

#### Scenario: Previously compiled test build
- **WHEN** the configured release already exists before the round-trip test starts
- **THEN** the freshness-dependent test fails clearly and directs the operator to use a new real build ID
- **AND** it does not report queue processing as verified by that record

## ADDED Requirements

### Requirement: Queue lifecycle verification
Focused queue tests SHALL verify transport serialization, eventual processing, duplicate delivery, and retry exhaustion using isolated test queues and test-owned records. A running Functions-host check SHALL verify that an accepted request can be processed by the registered queue trigger. Test cleanup SHALL NOT delete shared queues or release data.

#### Scenario: Actual trigger processes submitted work
- **WHEN** a running Functions host accepts compilation for a previously absent test-owned release
- **THEN** the registered queue trigger persists that release without directly invoking the worker from the test

#### Scenario: Poison-message lifecycle
- **WHEN** an isolated host receives a message that fails all five dequeue attempts
- **THEN** the message appears in the test poison queue and the test fails if it is silently discarded
