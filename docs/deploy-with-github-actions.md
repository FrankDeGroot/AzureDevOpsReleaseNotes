# 3. Deploy with GitHub Actions

After the repository variables and federated credential are configured, push to `main` or use **Run workflow** for [.github/workflows/azure-static-web-apps.yml](../.github/workflows/azure-static-web-apps.yml). The workflow authenticates with Azure, deploys Bicep, and runs the Release unit tests (`Category!=Integration`). It then deploys the Functions API and runs the integration tests through the Static Web App. The Static Web App's linked backend makes the Function App reject direct calls, so the tests can't call the Function App's own URL. The step retrieves and masks the default function key, sets `RELEASE_NOTES_API_URL` to `https://<static-web-app-hostname>/api`, waits for the API to respond, and runs `tests/Api.IntegrationTests`. The tests send the key as the `code` query parameter, because the Static Web App proxy drops the `x-functions-key` header. Any integration failure fails the run before the Blazor client is uploaded.

Before each deployed integration run, prepare a fresh Azure DevOps build and refresh the repository variables so `AZURE_DEVOPS_BUILD_ID` identifies a release that is not already in Table Storage:

```powershell
. ./.env.ps1
./scripts/Initialize-AzureDevOpsTestProject.ps1 -NewBuild
./scripts/Sync-Environment.ps1
```

The test rejects a cached release instead of treating it as proof of queue processing; do not delete stored releases. The workflow continues to retrieve and mask the Function key at runtime and pass it as the `code` query parameter. A 120-second polling timeout reports the release ID and last response; inspect Function processing logs and the poison queue before retrying with another fresh build.