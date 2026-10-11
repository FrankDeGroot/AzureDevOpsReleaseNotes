# 1. Configure Azure OIDC for GitHub Actions

Create or select a Microsoft Entra application and service principal for this repository. Create a GitHub Actions federated identity credential restricted to this repository and the `main` branch, then grant the service principal `Contributor` at the subscription scope so it can create the resource group and workload resources.

In **Settings > Secrets and variables > Actions > Variables**, configure these repository variables:

- `AZURE_CLIENT_ID`: Application (client) ID of the Entra application.
- `AZURE_TENANT_ID`: Microsoft Entra tenant ID.
- `AZURE_SUBSCRIPTION_ID`: Subscription that will contain the deployment.
- `AZURE_LOCATION`: Azure region for the deployment.
- `AZURE_RESOURCE_SUFFIX`: A globally unique, 3-24 character lowercase alphanumeric suffix, such as `contoso123`. It is used in the Static Web App and Cosmos DB account names.
- `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, `AZURE_DEVOPS_BUILD_ID`: The Azure DevOps test build that the post-deploy integration tests compile. [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md) writes these keys to `.env`.

Rather than entering these variables by hand, put them in `.env` and run `./scripts/Sync-Environment.ps1` (see [Environment configuration](environment-configuration.md)).

The workflow uses GitHub OpenID Connect with these values. Do not configure `AZURE_STATIC_WEB_APPS_API_TOKEN`; the workflow retrieves and masks the deployment token at runtime after Bicep provisions the Static Web App.