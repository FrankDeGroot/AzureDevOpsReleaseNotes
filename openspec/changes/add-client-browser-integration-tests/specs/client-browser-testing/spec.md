# Spec Delta

## Purpose

Provides browser-level verification of the Blazor client, including its rendered release views and navigation, while supporting live API checks by default and explicitly selected isolated UI checks.

## ADDED Requirements

### Requirement: Browser tests exercise the release workflow
The browser test suite SHALL use a real browser to load the client, display release data, and navigate between the release list and a release detail view.

#### Scenario: Display releases from the API
- **WHEN** the client loads with a reachable API containing releases
- **THEN** the browser displays release rows with their build, project, date, and change counts

#### Scenario: Navigate to and from release details
- **WHEN** a user opens a release row and then selects the return-to-list link
- **THEN** the browser displays that release's details and returns to the release list

### Requirement: Live API mode is the default
The browser suite SHALL send the client's API requests to the configured live API path unless stub mode is explicitly selected.

#### Scenario: Run without a mode setting
- **WHEN** the browser suite runs without an API mode setting
- **THEN** API requests reach the live API and any client or API failure fails the test

#### Scenario: Live API is unavailable
- **WHEN** a live API request fails or the configured client cannot load
- **THEN** the test fails without switching to stub responses

### Requirement: Stub responses require explicit selection
The browser suite SHALL provide deterministic stubbed API responses only when the test configuration explicitly selects stub mode.

#### Scenario: Run with stub mode selected
- **WHEN** the browser suite runs with `CLIENT_API_MODE=stub`
- **THEN** matching API requests receive configured stub responses without reaching the live API

#### Scenario: Run with another or absent mode
- **WHEN** the browser suite runs with the mode unset or set to live
- **THEN** stub interception is not enabled