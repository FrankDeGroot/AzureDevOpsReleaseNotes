# Spec Delta

## Purpose

Defines how developer and CI configuration flows from a single local `.env` file into PowerShell session variables and GitHub Actions repository variables and secrets, and how that setup is documented.

## ADDED Requirements

### Requirement: Committed example environment file
The repository SHALL include a committed `.env.example.ps1`. It SHALL list every supported key, using placeholder values only and a short comment for each key. Real `.env` and `.env.ps1` files SHALL stay ignored by Git.

#### Scenario: Example is tracked, real files are ignored
- **WHEN** a developer checks `git status` after creating `.env` and `.env.ps1`
- **THEN** `.env.example.ps1` is tracked and `.env` and `.env.ps1` are ignored

#### Scenario: Example contains no secrets
- **WHEN** `.env.example.ps1` is inspected
- **THEN** every value is a placeholder and no real credential, key, or tenant-specific identifier appears

### Requirement: Generate .env.ps1 from .env
A PowerShell 7 sync script SHALL generate `.env.ps1` from `.env`. Each `KEY=value` line becomes an assignment `$env:KEY = '<value>'`, with the value escaped so that it is treated literally. Blank lines and `#` comments in `.env` SHALL be ignored.

#### Scenario: Values with special characters
- **WHEN** `.env` contains a value with quotes, `$`, or `=` characters
- **THEN** dot-sourcing the generated `.env.ps1` sets the environment variable to exactly that value

#### Scenario: Regeneration
- **WHEN** the sync script runs again after `.env` changes
- **THEN** `.env.ps1` is overwritten to match `.env`

### Requirement: Sync GitHub Actions variables and secrets from .env
The sync script SHALL set GitHub repository Actions variables and secrets from `.env` with the GitHub CLI:
- Keys whose names contain `SECRET`, `TOKEN`, `PASSWORD`, or `KEY` SHALL become secrets.
- Other keys SHALL become variables.
- Local-only keys (`RELEASE_NOTES_API_URL`, `FUNCTION_KEY`) SHALL be skipped.

Secret values SHALL NOT be printed.

#### Scenario: Variables and secrets pushed
- **WHEN** the sync script runs with an authenticated `gh` session and a `.env` containing `AZURE_DEVOPS_BUILD_ID` and `CLIENT_SECRET`
- **THEN** `AZURE_DEVOPS_BUILD_ID` is set as a repository variable, `CLIENT_SECRET` is set as a repository secret, and the output shows the secret's name but not its value

#### Scenario: Local-only keys skipped
- **WHEN** `.env` contains `RELEASE_NOTES_API_URL` and `FUNCTION_KEY`
- **THEN** neither is pushed to GitHub

#### Scenario: Re-run is idempotent
- **WHEN** the sync script runs twice with an unchanged `.env`
- **THEN** the GitHub variables and secrets keep the same values and no error occurs

#### Scenario: Local-only mode
- **WHEN** the sync script runs with the option to skip GitHub
- **THEN** it regenerates `.env.ps1` and makes no GitHub calls

#### Scenario: gh not authenticated
- **WHEN** the GitHub sync step runs without an authenticated `gh` session
- **THEN** the script fails with an instruction to run `gh auth login`, after `.env.ps1` has already been regenerated

### Requirement: README setup instructions
The README SHALL document:
- the prerequisites
- creating `.env` from the example keys
- running the Azure DevOps setup script
- running the sync script
- running the integration tests locally against the emulator
- how the GitHub Actions workflow runs them after deployment

#### Scenario: New contributor follows the README
- **WHEN** a contributor with access to Azure, Azure DevOps, and the GitHub repository follows the README setup steps in order
- **THEN** they can run the integration tests locally, and the GitHub Actions workflow has every variable it needs to run them after deployment
