# Spec Delta

## Purpose

Stores and retrieves structured release notes documents in Azure Cosmos DB for fast querying and historical tracking.

## ADDED Requirements

### Requirement: Persist release note documents
The system SHALL persist compiled release note records in an Azure Cosmos DB container with deterministic document identifiers partitioned by project or repository ID.

#### Scenario: Successfully storing a release note
- **WHEN** a release note document is compiled
- **THEN** it is upserted into the Cosmos DB container with build details, work item metadata, and commit summaries

### Requirement: Query release notes
The system SHALL support retrieving release notes by project and build ID, or listing recent releases.

#### Scenario: Fetch release by ID
- **WHEN** a client queries `/api/releases/{id}`
- **THEN** the system returns the corresponding release note document or HTTP 404 if not found

#### Scenario: List releases for a project
- **WHEN** a client queries `/api/releases?project={projectName}`
- **THEN** the system returns an array of release summaries ordered chronologically descending
