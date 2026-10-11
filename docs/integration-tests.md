# Integration tests

[tests/Api.IntegrationTests](../tests/Api.IntegrationTests) calls a running API over HTTP. The tests check that the API reaches Cosmos DB and Azure DevOps: they compile release notes for the Azure DevOps test build, read the stored record back by ID and in the project list, re-compile to confirm the record is not duplicated, and check that `GET /api/health` reports both dependencies healthy. The tests carry the `Category=Integration` trait and are excluded from the regular unit-test run.

Locally, the API uses the Cosmos DB emulator and the real Azure DevOps service through your `az login`:

1. Set up the Azure DevOps test project once (see [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md)) and run `./scripts/Sync-Environment.ps1 -SkipGitHub`.
2. Start the API: `az login`, then `cd src/Api && dotnet run`.
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

Don't run `dotnet test` on the whole solution while the local host is running. Rebuilding `src/Api` under a running host breaks its function registrations, and you have to restart the host.