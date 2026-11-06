# Spec Delta

## MODIFIED Requirements

### Requirement: Trigger compilation via HTTP endpoint
The system SHALL expose an authorized HTTP POST endpoint (`/api/releases/compile`) accepting organization, project, build ID, repository ID, and optional project ID. Valid requests SHALL be normalized and durably queued before returning HTTP 202 with JSON fields `id`, `projectId`, and `releaseUrl`, plus a matching `Location` header. The handler SHALL NOT query Azure DevOps or persist a release.

#### Scenario: Valid pipeline trigger request
- **WHEN** a valid JSON payload with build metadata and authorization is posted to `/api/releases/compile`
- **THEN** the system queues the request and returns HTTP 202
- **AND** `id` equals `{projectId}:{buildId}`, and `releaseUrl` and `Location` identify the existing release GET endpoint with escaped ID and project ID
- **AND** the response contains no compiled commits or work items

#### Scenario: Missing required build metadata
- **WHEN** a request is received missing required fields (such as `buildId` or `project`) or with a non-positive build ID
- **THEN** the system returns HTTP 400 Bad Request with descriptive validation errors
- **AND** no compilation message is submitted

#### Scenario: Malformed JSON
- **WHEN** the request body is malformed JSON
- **THEN** the system returns HTTP 400 with the existing JSON error shape and no queued message

#### Scenario: Missing optional project ID
- **WHEN** valid metadata omits `projectId` or supplies a blank value
- **THEN** the project name is used as the project ID in the queued request, release ID, and acknowledgment

#### Scenario: Unauthorized submission
- **WHEN** a deployed caller lacks valid Function authorization
- **THEN** the request is rejected before a compilation message is submitted

#### Scenario: Queue submission fails
- **WHEN** queue submission fails because the storage service is unavailable
- **THEN** the endpoint returns HTTP 503 with a JSON `error` and logs the failure without credentials
- **AND** it does not return an acceptance acknowledgment

## ADDED Requirements

### Requirement: Release reads expose availability rather than job status
Callers SHALL use the existing release GET endpoint to read available results. Reads SHALL retain existing HTTP 404 behavior before first persistence. An existing release SHALL remain readable while recompilation is queued or running. HTTP 202 SHALL indicate submission only, not completion or guaranteed eventual success.

#### Scenario: First compilation still pending
- **WHEN** a caller follows the acknowledged release URL before a release has been persisted
- **THEN** the GET endpoint returns the existing HTTP 404 error
- **AND** it returns the release document after background persistence succeeds

#### Scenario: Existing release is recompiled
- **WHEN** a caller submits a build with a previously persisted release
- **THEN** reads can return that previous document immediately
- **AND** a successful GET does not establish that the latest submission completed

#### Scenario: Compilation permanently fails
- **WHEN** accepted work exhausts background retries without storing its first release
- **THEN** release GET continues to return HTTP 404
- **AND** the caller must use a bounded polling timeout and operator diagnostics rather than interpret acceptance as success
