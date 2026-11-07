# release-notes-compiler Specification

## Purpose

Compiles release notes by querying Azure DevOps REST API for commits and associated work items for a specific build run.

## Requirements

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

### Requirement: Fetch changes and work items from Azure DevOps
The system SHALL query the Azure DevOps REST API for the build's changes and linked work items using the provided access token.

#### Scenario: Successful Azure DevOps query
- **WHEN** querying Azure DevOps REST APIs with valid credentials
- **THEN** all associated commits and work items (with titles, IDs, states, and types) are retrieved and aggregated