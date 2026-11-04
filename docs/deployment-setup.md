# Deployment Setup

The Bicep entry point at [infra/main.bicep](../infra/main.bicep) creates resource group `rg-azure-devops-release-notes` in the configured Azure region. It deploys an Azure Static Web App, an Azure Functions Flex Consumption app, its StorageV2 host account, and Application Insights backed by a Log Analytics workspace. The same StorageV2 account hosts the release-notes table.

The workload module at [infra/workload.bicep](../infra/workload.bicep) provisions the `releases` table and configures the Function App's `Storage__TableEndpoint` and `Storage__TableName` settings. The Function App's system-assigned identity already has the Storage Table Data Contributor role on the host account; no storage key is placed in app settings. The table starts empty and existing Cosmos DB records are not copied. Removing the Cosmos resource declaration from the incremental Bicep template does not delete an already-provisioned Cosmos account, so it remains available for rollback or manual retention.

1. [Configure Azure OIDC for GitHub Actions](configure-azure-oidc-for-github-actions.md)
2. [Provision with Azure CLI](provision-with-azure-cli.md)
3. [Deploy with GitHub Actions](deploy-with-github-actions.md)
4. [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md)