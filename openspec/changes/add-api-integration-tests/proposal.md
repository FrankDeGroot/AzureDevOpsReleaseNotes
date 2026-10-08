# Proposal

## Why

The API's only tests are unit tests with fakes, so nothing verifies that the Functions API can actually reach Cosmos DB and the Azure DevOps REST API, either on a developer machine or after deployment. The example Azure DevOps pipeline in `pipelines/azure-pipelines.yml` only shows how a caller might invoke the API. It does not give us a repeatable Azure DevOps project, build, or work items to test against. Configuration is also spread across a hand-written `.env`, a hand-written `.env.ps1`, and manually entered GitHub repository variables.

## What Changes

- Add a black-box HTTP integration test suite that calls a running API by base URL. It checks the `health` endpoint for both dependencies, compiles release notes for a known Azure DevOps build (this calls Azure DevOps and writes to Cosmos DB), and reads the stored record back from Cosmos DB through `GET releases/{id}` and `GET releases`.
- Run the suite locally against the Functions host on `localhost:7071`. Cosmos DB comes from the dev-container emulator, and Azure DevOps is the real cloud service, reached through the developer's `az login`.
- Exclude the suite from the existing unit-test step in GitHub Actions. Run it after the Function App is deployed, against the deployed Function App URL, using a function key retrieved at runtime.
- **BREAKING**: Remove `pipelines/azure-pipelines.yml`. Replace it with an idempotent PowerShell script that creates or reuses an Azure DevOps project, Git repo, seed commit, linked work item, YAML pipeline, and a completed build. The script also grants the deployed Function App's managed identity read access to that project. It writes the resulting IDs back to `.env`.
- Add a committed example `.env.example.ps1` (the real `.env` / `.env.ps1` stay ignored). Add a PowerShell script that reads `.env` and then:
  - pushes GitHub Actions repository variables and secrets with `gh`;
  - regenerates `.env.ps1`.
- Update the README with setup instructions for the Azure DevOps test project, local integration test runs, and GitHub configuration sync. Remove the "Configure Azure DevOps Pipeline" section that references the deleted YAML.

## Capabilities

### New Capabilities
- `api-integration-tests`: Black-box integration tests for the Functions API against Cosmos DB and Azure DevOps. Covers local and deployed targets, and how the suite runs in GitHub Actions.
- `azure-devops-test-environment`: Idempotent provisioning of the Azure DevOps project, repo, work item, pipeline, build, and managed-identity access that the integration tests depend on.
- `environment-configuration`: The `.env` contract, the example `.env.ps1`, and syncing `.env` to `.env.ps1` and to GitHub Actions variables and secrets, plus the README setup instructions.

### Modified Capabilities
<!-- None: openspec/specs/ has no archived capabilities yet. The in-flight add-bicep-github-deployment change owns the workflow requirements; this change adds to that workflow without redefining it. -->

## Impact

- **Tests**: new `tests/Api.IntegrationTests` project, added to `ReleaseNotes.slnx`. The unit-test step in the workflow filters it out.
- **Workflow**: `.github/workflows/azure-static-web-apps.yml` gains a post-deploy integration-test step and new repository variables for the Azure DevOps test target.
- **Scripts**: new `scripts/Initialize-AzureDevOpsTestProject.ps1` and `scripts/Sync-Environment.ps1`. These require PowerShell 7, the Azure CLI (used for tokens and Function App lookups; the `azure-devops` extension is not needed), and the GitHub CLI (`gh`).
- **Removed**: `pipelines/azure-pipelines.yml`.
- **Config**: `.gitignore` gains an exception for `.env.example.ps1`. The `.env` key names are normalized; the legacy `TENANT_ID` / `CLIENT_ID` / `CLIENT_SECRET` / `BUILD_ID` / `REPOSITORY_ID` keys are replaced.
- **Azure DevOps**: the setup script adds the Function App managed identity to the organization (Basic or Stakeholder access) and to the test project's Readers group.
- **API fix**: the integration tests found that `AzureDevOpsClient` failed on the string work-item IDs returned by the build work items API. The parsing is fixed, with a unit test. Otherwise the API is unchanged: it already uses `DefaultAzureCredential` locally and managed identity in Azure.
