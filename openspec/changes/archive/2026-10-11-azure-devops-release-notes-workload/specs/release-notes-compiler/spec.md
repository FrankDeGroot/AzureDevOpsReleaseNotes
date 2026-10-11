# Spec Delta

## Purpose

Compiles release notes by querying Azure DevOps REST API for commits and associated work items for a specific build run.

## ADDED Requirements

### Requirement: Trigger compilation via HTTP endpoint
The system SHALL expose an HTTP POST endpoint (`/api/releases/compile`) that accepts build compilation requests containing Azure DevOps organization details, project name, build ID, and repository ID.

#### Scenario: Valid pipeline trigger request
- **WHEN** a valid JSON payload with build metadata and authorization is posted to `/api/releases/compile`
- **THEN** the system fetches commits and linked work items from Azure DevOps, generates a release note document, saves it to Cosmos DB, and returns HTTP 200 with the compiled release summary

#### Scenario: Missing required build metadata
- **WHEN** a request is received missing required fields (such as `buildId` or `project`)
- **THEN** the system returns HTTP 400 Bad Request with descriptive validation errors

### Requirement: Fetch changes and work items from Azure DevOps
The system SHALL query the Azure DevOps REST API for the build's changes and linked work items using the provided access token.

#### Scenario: Successful Azure DevOps query
- **WHEN** querying Azure DevOps REST APIs with valid credentials
- **THEN** all associated commits and work items (with titles, IDs, states, and types) are retrieved and aggregated
