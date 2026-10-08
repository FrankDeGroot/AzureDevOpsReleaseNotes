# AzureDevOpsReleaseNotes
Azure DevOps release notes tooling with .NET and Node.js development environments.

## Local Emulators

Run **Dev Containers: Rebuild Container** to start Azurite and the Linux Cosmos DB NoSQL emulator alongside the workspace. Docker Compose must be available on the host. The Cosmos emulator is a preview and supports gateway mode, which the API already uses. Run the application from the dev container so it can reach both emulators.

The container configures `AzureWebJobsStorage=UseDevelopmentStorage=true` and the API's Cosmos DB settings. A startup hook trusts the Cosmos emulator's HTTPS certificate on every start. The key in [.devcontainer/cosmos-emulator.key](.devcontainer/cosmos-emulator.key) is public and for local development only.

- Azurite Blob, Queue, and Table endpoints: `http://localhost:10000`, `http://localhost:10001`, and `http://localhost:10002`.
- Cosmos DB endpoint: `https://localhost:8081`.
- Cosmos Data Explorer: `http://localhost:1234`.

Both emulators persist data in Docker named volumes. Rebuilding the container preserves this data; removing the Compose volumes resets it.

### Run the app

The dev container includes the .NET SDK, Azure Functions Core Tools, and Static Web Apps CLI. From the repository root, restore the solution and create the Functions host's local settings file:

```bash
dotnet restore ReleaseNotes.slnx
cp src/Api/local.settings.example.json src/Api/local.settings.json
```

Sign in to Azure CLI so the API can use your developer identity when calling Azure DevOps:

```bash
az login
```

Start each process in its own terminal from the repository root:

```bash
dotnet run --project src/Client --launch-profile http
```

```bash
cd src/Api && dotnet run
```

```bash
swa start http://localhost:5017 --api-devserver-url http://localhost:7071
```

Open `http://localhost:4280`. The Static Web Apps CLI serves the local app and routes `/api` requests to Functions. The API uses Azure CLI credentials in Development; in Azure it continues to use managed identity. Compiling release notes requires an Azure DevOps identity with access to the target organization and project. Application Insights is optional for local runs.

## Environment configuration

Keep all settings in a `.env` file (`KEY=value` per line) at the repository root. [.env.example.ps1](.env.example.ps1) lists every supported key with placeholder values. `.env` and `.env.ps1` are gitignored. After editing `.env`, regenerate `.env.ps1` and push the GitHub Actions configuration:

```powershell
./scripts/Sync-Environment.ps1             # writes .env.ps1 and sets GitHub variables and secrets
./scripts/Sync-Environment.ps1 -SkipGitHub # writes .env.ps1 only
. ./.env.ps1                               # load the values into the current PowerShell session
```

[scripts/Sync-Environment.ps1](scripts/Sync-Environment.ps1) requires `gh auth login`. It sets keys whose names contain `SECRET`, `TOKEN`, `PASSWORD`, or `KEY` as repository secrets and all other keys as repository variables. It skips empty keys and the local-only keys `RELEASE_NOTES_API_URL` and `FUNCTION_KEY`. Use `-Repo owner/name` to target a different repository.

If your `.env` uses the older keys, rename `TENANT_ID` and `CLIENT_ID` to `AZURE_TENANT_ID` and `AZURE_CLIENT_ID`, and remove `CLIENT_SECRET`, `BUILD_ID`, and `REPOSITORY_ID`. The Azure DevOps setup script writes the replacement `AZURE_DEVOPS_*` keys. `RELEASE_NOTES_API_URL` is now the API base URL (for example `http://localhost:7071/api`), not the compile endpoint.

## Integration tests

[tests/Api.IntegrationTests](tests/Api.IntegrationTests) calls a running API over HTTP. The tests check that the API reaches Cosmos DB and Azure DevOps: they compile release notes for the Azure DevOps test build, read the stored record back by ID and in the project list, re-compile to confirm the record is not duplicated, and check that `GET /api/health` reports both dependencies healthy. The tests carry the `Category=Integration` trait and are excluded from the regular unit-test run.

Locally, the API uses the Cosmos DB emulator and the real Azure DevOps service through your `az login`:

1. Set up the Azure DevOps test project once (see [Set up the Azure DevOps test project](#4-set-up-the-azure-devops-test-project)) and run `./scripts/Sync-Environment.ps1 -SkipGitHub`.
2. Start the API: `az login`, then `cd src/Api && dotnet run`.
3. In another PowerShell terminal at the repository root:

   ```powershell
   . ./.env.ps1
   dotnet test tests/Api.IntegrationTests --filter Category=Integration
   ```

| Variable | Purpose |
| --- | --- |
| `RELEASE_NOTES_API_URL` | API base URL. Defaults to `http://localhost:7071/api`. |
| `FUNCTION_KEY` | Optional. Sent as `x-functions-key`. The local host does not require it. |
| `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, `AZURE_DEVOPS_BUILD_ID` | The test build, written by the setup script. When any of these is missing, the tests fail and name the missing keys. |

Don't run `dotnet test` on the whole solution while the local host is running. Rebuilding `src/Api` under a running host breaks its function registrations, and you have to restart the host.

## Deployment

The application deploys to **Azure Static Web Apps** via the GitHub Actions workflow located at [.github/workflows/azure-static-web-apps.yml](.github/workflows/azure-static-web-apps.yml).

### Architecture
- **Frontend**: Blazor WebAssembly ([src/Client](src/Client))
- **Backend**: Azure Functions isolated worker ([src/Api](src/Api))
- **Shared**: Domain models ([src/Shared](src/Shared))

## Deployment Setup

The Bicep entry point at [infra/main.bicep](infra/main.bicep) creates resource group `rg-azure-devops-release-notes` in the configured Azure region. It deploys a Free Azure Static Web App, a Cosmos DB SQL API account using free tier and serverless capacity, and an Azure Application Insights instance backed by a Log Analytics workspace. Cosmos free tier is limited to one account per subscription, and serverless usage remains consumption-based.

The workload module at [infra/workload.bicep](infra/workload.bicep) configures the Functions API with its Cosmos connection string, `release-notes` database name, `releases` container name, and Application Insights connection string. The application creates the database and container on first use; no manual portal configuration is needed.

### 1. Configure Azure OIDC for GitHub Actions

Create or select a Microsoft Entra application and service principal for this repository. Create a GitHub Actions federated identity credential restricted to this repository and the `main` branch, then grant the service principal `Contributor` at the subscription scope so it can create the resource group and workload resources.

In **Settings > Secrets and variables > Actions > Variables**, configure these repository variables:

- `AZURE_CLIENT_ID`: Application (client) ID of the Entra application.
- `AZURE_TENANT_ID`: Microsoft Entra tenant ID.
- `AZURE_SUBSCRIPTION_ID`: Subscription that will contain the deployment.
- `AZURE_LOCATION`: Azure region for the deployment.
- `AZURE_RESOURCE_SUFFIX`: A globally unique, 3-24 character lowercase alphanumeric suffix, such as `contoso123`. It is used in the Static Web App and Cosmos DB account names.
- `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, `AZURE_DEVOPS_BUILD_ID`: The Azure DevOps test build that the post-deploy integration tests compile. [Set up the Azure DevOps test project](#4-set-up-the-azure-devops-test-project) writes these keys to `.env`.

Rather than entering these variables by hand, put them in `.env` and run `./scripts/Sync-Environment.ps1` (see [Environment configuration](#environment-configuration)).

The workflow uses GitHub OpenID Connect with these values. Do not configure `AZURE_STATIC_WEB_APPS_API_TOKEN`; the workflow retrieves and masks the deployment token at runtime after Bicep provisions the Static Web App.

### 2. Provision with Azure CLI

To provision the infrastructure outside GitHub Actions, sign in to the intended subscription and run:

```bash
az login
az account set --subscription "<subscription-id>"
az deployment sub create \
	--name release-notes-local \
	--location "<azure-region>" \
	--template-file infra/main.bicep \
	--parameters location="<azure-region>" resourceNameSuffix="<lowercase-alphanumeric-suffix>"
```

This command creates or updates the resource group and workload without requiring a pre-existing resource group. [infra/main.bicepparam](infra/main.bicepparam) shows the parameter contract; replace its example suffix before using it.

### 3. Deploy with GitHub Actions

After the repository variables and federated credential are configured, push to `main` or use **Run workflow** for [.github/workflows/azure-static-web-apps.yml](.github/workflows/azure-static-web-apps.yml). The workflow authenticates with Azure, deploys Bicep, and runs the Release unit tests (`Category!=Integration`). It then deploys the Functions API and runs the integration tests against the deployed Function App: it retrieves and masks the default function key, sets `RELEASE_NOTES_API_URL` to `https://<function-app-hostname>/api`, waits for the API to respond, and runs `tests/Api.IntegrationTests`. Any integration failure fails the run before the Blazor client is uploaded.

### 4. Set up the Azure DevOps test project

The integration tests compile release notes for a real Azure DevOps build. [scripts/Initialize-AzureDevOpsTestProject.ps1](scripts/Initialize-AzureDevOpsTestProject.ps1) idempotently creates or reuses:

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
