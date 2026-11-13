# Integration tests

[tests/Api.IntegrationTests](../tests/Api.IntegrationTests) calls a running API over HTTP. The tests verify an HTTP 202 compile acknowledgment and matching `Location`, wait for the initially absent release to become available, read it back with and without a project ID, list project releases, submit it again, and check that `GET /api/health` reports Table Storage and Azure DevOps healthy. Polling retries only 404, uses a 120-second overall deadline with one-second delays, and fails immediately on other HTTP statuses or invalid release JSON. The tests carry the `Category=Integration` trait and are excluded from the regular unit-test run.

Locally, the API uses Azurite Table Storage and Queue Storage, and the real Azure DevOps service through your `az login`:

1. Set up the Azure DevOps test project (see [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md)). Before each live run, create a fresh qualifying build and refresh the local environment:

   ```powershell
   . ./.env.ps1
   ./scripts/Initialize-AzureDevOpsTestProject.ps1 -NewBuild
   ./scripts/Sync-Environment.ps1 -SkipGitHub
   . ./.env.ps1
   ```

   The integration fixture rejects a build ID whose release already exists. Use `-NewBuild` to produce a new linked commit/build; do not delete Table Storage records to force an initially absent release.
2. Ensure the dev container is running Azurite (see [Local emulators](local-emulators.md)), sign in with `az login`, then start the API with `cd src/Api && dotnet run`.
3. In another PowerShell terminal at the repository root:

   ```powershell
   . ./.env.ps1
   dotnet test tests/Api.IntegrationTests --filter Category=Integration
   ```

| Variable | Purpose |
| --- | --- |
| `RELEASE_NOTES_API_URL` | API base URL. Defaults to `http://localhost:7071/api`. |
| `FUNCTION_KEY` | Optional. Sent as the `code` query parameter. The local host does not require it. |
| `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, `AZURE_DEVOPS_BUILD_ID` | The test build, written by the setup script. When any of these is missing, the tests fail and name the missing keys. |

On timeout, inspect the reported release ID and last GET response, then check Functions processing logs and `release-note-compilation-poison`. A prior record is intentionally left readable during recompilation, so only a fresh build ID can prove first persistence.

Don't run `dotnet test` on the whole solution while the local host is running. Rebuilding `src/Api` under a running host breaks its function registrations, and you have to restart the host.