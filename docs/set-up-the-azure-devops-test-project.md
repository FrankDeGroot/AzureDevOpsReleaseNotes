# 4. Set up the Azure DevOps test project

The integration tests compile release notes for a real Azure DevOps build. [scripts/Initialize-AzureDevOpsTestProject.ps1](../scripts/Initialize-AzureDevOpsTestProject.ps1) idempotently creates or reuses:

- the project (default `ReleaseNotesIntegration`, Basic process) and its default Git repository;
- a seed `Task` work item tagged `release-notes-integration-seed`, linked to the seed commit;
- an agentless YAML pipeline `release-notes-integration` (no hosted agent parallelism required);
- a succeeded build whose changes include a commit and whose work items include the seed item.

It also grants the deployed Function App's managed identity **Basic** access to the organization and adds it to the project's **Readers** group, so the deployed API can read builds, commits, and work items. Basic access uses one of the organization's Basic seats (the first five are free). Finally, it writes `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, and `AZURE_DEVOPS_BUILD_ID` to `.env`.

Prerequisites: PowerShell 7, and `az login` as a user who can create projects in the organization and manage its users. No personal access token is needed.

```powershell
. ./.env.ps1
./scripts/Initialize-AzureDevOpsTestProject.ps1 -Organization <organization>
./scripts/Sync-Environment.ps1
```

The Function App name defaults to `func-release-notes-$env:AZURE_RESOURCE_SUFFIX`; override it with `-FunctionAppName`. When the app does not exist yet, the script skips the identity grant with a warning. On a fresh environment, use this order: deploy with the workflow, run the setup script so the new managed identity gets access, then re-run the workflow so the integration tests pass.

<!-- End of deployment setup. -->