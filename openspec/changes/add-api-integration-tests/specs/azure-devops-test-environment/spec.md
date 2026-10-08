# Spec Delta

## Purpose

Provides a repeatable, idempotent way to stand up the Azure DevOps project and build data that the API integration tests compile release notes from. The same process grants the deployed API identity read access to that data.

## ADDED Requirements

### Requirement: Idempotent Azure DevOps test project setup
A PowerShell 7 script SHALL create the following in the configured organization, or reuse them if they already exist:
- the named Azure DevOps project
- a Git repository
- a seed commit
- a work item linked to that commit
- a YAML pipeline

Re-running the script SHALL NOT create duplicates of any of these.

#### Scenario: First run on an empty organization
- **WHEN** the script runs against an organization where the configured project does not exist
- **THEN** it creates the project, repository, seed commit, linked work item, and pipeline

#### Scenario: Re-run on an existing setup
- **WHEN** the script runs again with the same configuration
- **THEN** it reuses the existing project, repository, work item, and pipeline and creates no duplicates

### Requirement: Completed build with commits and work items
The script SHALL make sure the pipeline has a successfully completed build whose changes include at least one commit and whose linked work items include at least one work item. If no such build exists, the script SHALL queue one and wait for it to complete. If one exists, the script SHALL reuse it.

#### Scenario: No qualifying build
- **WHEN** the pipeline has no successful build with associated commits and work items
- **THEN** the script queues a build, waits for completion, and fails with a clear message if the build does not succeed

#### Scenario: Qualifying build exists
- **WHEN** a successful build with associated commits and work items already exists
- **THEN** the script reuses it without queuing another build

### Requirement: Setup outputs written to .env
The script SHALL write these keys to `.env`, updating existing keys in place and preserving all other keys:
- `AZURE_DEVOPS_ORGANIZATION`
- `AZURE_DEVOPS_PROJECT`
- `AZURE_DEVOPS_PROJECT_ID`
- `AZURE_DEVOPS_REPOSITORY_ID`
- `AZURE_DEVOPS_BUILD_ID`

#### Scenario: Existing .env with other keys
- **WHEN** the script completes and `.env` already contains unrelated keys
- **THEN** the Azure DevOps keys are added or updated and the unrelated keys are unchanged

### Requirement: Deployed API identity granted read access
When a Function App name is provided or can be resolved from the configured Azure resource suffix, the script SHALL grant that app's managed identity two things: access to the organization, and membership in the test project's Readers group. Re-running the script SHALL NOT create duplicate grants. When no Function App can be resolved, the script SHALL skip this step with a warning.

#### Scenario: Function App deployed
- **WHEN** the script runs after the Function App has been deployed
- **THEN** the Function App's managed identity can read builds, changes, and work items in the test project

#### Scenario: Function App not yet deployed
- **WHEN** no Function App can be resolved
- **THEN** the script completes the project setup, warns that identity access was skipped, and exits successfully

### Requirement: Setup uses the signed-in Azure CLI identity
The script SHALL authenticate to Azure DevOps with the current Azure CLI sign-in. It SHALL NOT require a personal access token. If no Azure CLI sign-in is available, it SHALL fail before making any changes.

#### Scenario: Not signed in
- **WHEN** the script runs without an active `az login` session
- **THEN** it exits with an error instructing the user to run `az login` and makes no changes

### Requirement: Example Azure DevOps YAML pipeline removed
The repository SHALL NOT contain the example `pipelines/azure-pipelines.yml`. The setup script SHALL be the documented way to obtain an Azure DevOps build for release-note compilation.

#### Scenario: Repository contents
- **WHEN** the change is applied
- **THEN** `pipelines/azure-pipelines.yml` no longer exists and the README references the setup script instead
