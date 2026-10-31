# Integration tests

[tests/Api.IntegrationTests](../tests/Api.IntegrationTests) checks a running API: dependency health, compilation, Cosmos read-back/listing, and repeated-compile idempotency. Live checks carry `Category=Integration`; unit and in-process host checks require no cloud credentials.

```bash
dotnet test tests/Api.Tests
```

## Local live checks

1. Follow [Local emulators](local-emulators.md), including readiness and HTTPS trust.
2. Set up the [Azure DevOps test project](set-up-the-azure-devops-test-project.md), sign in with `az login`, and run `pwsh scripts/Sync-Environment.ps1 -SkipGitHub`.
3. Start `dotnet run --project src/Api` from the repository root.
4. In a separate PowerShell terminal:

```powershell
. ./.env.ps1
dotnet test tests/Api.IntegrationTests --filter 'Category=Integration&Target!=Azure'
```

| Variable | Purpose |
| --- | --- |
| `RELEASE_NOTES_API_URL` | API base URL. Defaults to `http://localhost:7071/api`. |
| `RELEASE_NOTES_API_TOKEN` | Optional local-only bearer token for a deployed target. Leave empty for local Development. Never printed or sent in a URL. |
| `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, `AZURE_DEVOPS_BUILD_ID` | The test build, written by the setup script. When any of these is missing, the tests fail and name the missing keys. |

The host uses the developer's Azure CLI credentials for outbound Azure DevOps access. Local inbound compilation is anonymous only in Development outside App Service. Browser checks can run afterward using the compiled release as live data; see [Client browser tests](client-browser-tests.md).

## Deployed checks

CI obtains a short-lived Entra token for the configured API audience with its authorized federated identity and masks it before passing it to tests. An ARM token is not an API token. Function keys are no longer supported. Do not put tokens in query strings, source files, browser settings, or logs.

Easy Auth validates tokens at Azure's boundary; only compilation requires the resulting identity. Deployed checks must verify anonymous/invalid-token compilation is rejected without a redirect, public reads remain accessible, and authenticated compilation still completes. In-process tests verify local bypass and principal parsing but do not replace platform validation tests.

Azure-only tests carry `Target=Azure`. CI additionally passes a masked, short-lived ARM token as `RELEASE_NOTES_API_WRONG_AUDIENCE_TOKEN` solely to prove it is rejected by this API; it is never used for successful compilation. The expired/unsigned fixture verifies rejection of an untrusted token, not signed-token expiry validation in isolation. Tenant/principal boundaries are also covered by isolated host tests. Run Azure-only checks only against a deployed host with the required runtime tokens, using the full `Category=Integration` filter.