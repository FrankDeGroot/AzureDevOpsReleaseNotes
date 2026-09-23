# AzureDevOpsReleaseNotes
Azure DevOps release notes tooling with .NET and Node.js development environments.

## Deployment

The application deploys to **Azure Static Web Apps** via the GitHub Actions workflow located at [.github/workflows/azure-static-web-apps.yml](.github/workflows/azure-static-web-apps.yml).

### Architecture
- **Frontend**: Blazor WebAssembly ([src/Client](src/Client))
- **Backend**: Azure Functions isolated worker ([src/Api](src/Api))
- **Shared**: Domain models ([src/Shared](src/Shared))

## Deployment Setup

The Bicep entry point at [infra/main.bicep](infra/main.bicep) creates resource group `rg-azure-devops-release-notes` in the configured Azure region. It deploys a Free Azure Static Web App and a Cosmos DB SQL API account using free tier and serverless capacity. Cosmos free tier is limited to one account per subscription, and serverless usage remains consumption-based.

The workload module at [infra/workload.bicep](infra/workload.bicep) configures the Functions API with its Cosmos connection string, `release-notes` database name, and `releases` container name. The application creates the database and container on first use; no manual portal configuration is needed.

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
