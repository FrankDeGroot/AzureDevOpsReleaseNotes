# Spec Delta

## Purpose

Provides a responsive Blazor WebAssembly frontend on Azure Static Web Apps for browsing, searching, and viewing compiled release notes.

## ADDED Requirements

### Requirement: Release notes dashboard view
The system SHALL present a dashboard displaying a chronological list of compiled releases with project name, build number, date, and summary counts.

#### Scenario: User visits dashboard
- **WHEN** the user opens the Blazor application
- **THEN** the system loads and displays the latest release records retrieved from the backend API

### Requirement: Release note detail view
The system SHALL provide a detailed view for an individual release showing linked work items (with external links to Azure DevOps) and commit history.

#### Scenario: User selects a release
- **WHEN** the user clicks on a release in the dashboard
- **THEN** the application navigates to the detail page rendering formatted work items, commit logs, authors, and source branch metadata
