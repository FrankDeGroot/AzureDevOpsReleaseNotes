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

After the repository variables and federated credential are configured, push to `main` or use **Run workflow** for [.github/workflows/azure-static-web-apps.yml](.github/workflows/azure-static-web-apps.yml). The workflow authenticates with Azure, deploys Bicep, runs the Release test suite, and then uploads the Blazor client and integrated Functions API.

### 4. Configure Azure DevOps Pipeline
When integrating release notes compilation into your Azure DevOps build pipeline ([pipelines/azure-pipelines.yml](pipelines/azure-pipelines.yml)):
1. **Get the deployed hostname**: Use the `staticWebAppDefaultHostname` output from the `az deployment sub create` command, or query it later:
	 ```bash
	 az deployment sub show --name release-notes-local \
		 --query properties.outputs.staticWebAppDefaultHostname.value \
		 --output tsv
	 ```
2. **Set API Endpoint**: Update the `releaseNotesApiUrl` variable to `https://<STATIC-WEB-APP-HOSTNAME>/api/releases/compile`.
3. **Enable OAuth Token Access**: In Azure DevOps pipeline settings (or agent job options), ensure **Allow scripts to access the OAuth token** is enabled so `$(System.AccessToken)` is populated and authorized to query Azure DevOps REST APIs.

<!-- End of deployment setup. -->
