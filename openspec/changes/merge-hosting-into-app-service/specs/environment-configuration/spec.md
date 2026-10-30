# Spec Delta

## MODIFIED Requirements

### Requirement: Sync GitHub Actions variables and secrets from .env
The sync script SHALL set GitHub Actions configuration from `.env` using `gh`. Keys containing `SECRET`, `TOKEN`, `PASSWORD`, or `KEY` SHALL become secrets; others SHALL become variables. Local-only `RELEASE_NOTES_API_URL`, `RELEASE_NOTES_API_TOKEN`, and obsolete `FUNCTION_KEY` SHALL be skipped. Secret values SHALL NOT be printed.

#### Scenario: Variables and secrets pushed
- **WHEN** an authenticated sync processes `AZURE_DEVOPS_BUILD_ID` and `CLIENT_SECRET`
- **THEN** it sets the build ID as a variable and the client secret as a secret and prints no secret values

#### Scenario: Local-only keys skipped
- **WHEN** `.env` contains `RELEASE_NOTES_API_URL`, `RELEASE_NOTES_API_TOKEN`, or obsolete `FUNCTION_KEY`
- **THEN** none is pushed to GitHub
- **AND** the obsolete key does not enable Function-key authentication

#### Scenario: Re-run is idempotent
- **WHEN** the sync script runs twice with unchanged `.env` content
- **THEN** the GitHub values remain unchanged without error

#### Scenario: Local-only mode
- **WHEN** the sync script runs with the option to skip GitHub
- **THEN** it regenerates `.env.ps1` without GitHub calls

#### Scenario: gh not authenticated
- **WHEN** GitHub sync runs without an authenticated `gh` session
- **THEN** it fails with instructions to run `gh auth login` after regenerating `.env.ps1`

### Requirement: README setup instructions
The README SHALL document prerequisites, example environment keys, Azure DevOps setup, environment sync, local integration tests against the combined host and Cosmos emulator, and post-deployment GitHub Actions tests. It SHALL explain Easy Auth tenant/audience configuration, short-lived caller tokens, managed identity access, and the anonymous local Development exception.

#### Scenario: New contributor follows the README
- **WHEN** a contributor with Azure, Azure DevOps, and repository access follows setup in order
- **THEN** they can run anonymous local integration tests against the combined Development host and configure all required deployed authentication settings
- **AND** deployed tests use tokens minted at runtime instead of a stored Function key

## ADDED Requirements

### Requirement: Hosting authentication configuration is explicit
The committed example SHALL list placeholder tenant ID, API application ID, API audience, local API URL, and optional local-only API bearer token settings. Easy Auth settings SHALL be distinct from the deployment identity and outbound Azure DevOps identity. Azure deployment SHALL require tenant and API identifiers instead of silently disabling authentication.

#### Scenario: Configure hosted authentication
- **WHEN** a maintainer configures the example environment for Azure deployment
- **THEN** the documentation identifies `AZURE_TENANT_ID`, `RELEASE_NOTES_AUTH_CLIENT_ID`, and `RELEASE_NOTES_AUTH_AUDIENCE`, and explains how the federated deployment identity obtains an accepted token
- **AND** missing required authentication values stop deployment before publishing

#### Scenario: Local default configuration
- **WHEN** a developer uses the documented local settings
- **THEN** `RELEASE_NOTES_API_URL` targets `http://localhost:7071/api` and `RELEASE_NOTES_API_TOKEN` can remain empty
- **AND** the example contains no real credentials and no supported Function-key setting