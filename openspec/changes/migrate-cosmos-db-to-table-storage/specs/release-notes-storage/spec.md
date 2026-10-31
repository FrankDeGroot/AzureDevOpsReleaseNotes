# Spec Delta

## MODIFIED Requirements

### Requirement: Persist release note documents
The system SHALL persist compiled release note records in Azure Storage Tables with deterministic record identifiers derived from project or repository identity.

#### Scenario: Successfully storing a release note
- **WHEN** a release note document is compiled
- **THEN** it is upserted into the configured Azure Storage Table with build details, work item metadata, and commit summaries