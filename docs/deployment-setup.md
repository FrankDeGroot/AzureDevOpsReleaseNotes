# Deployment Setup

The Bicep entry point at [infra/main.bicep](../infra/main.bicep) creates resource group `rg-azure-devops-release-notes` in the configured Azure region. It deploys a Free Azure Static Web App, a Cosmos DB SQL API account using free tier and serverless capacity, and an Azure Application Insights instance backed by a Log Analytics workspace. Cosmos free tier is limited to one account per subscription, and serverless usage remains consumption-based.

The workload module at [infra/workload.bicep](../infra/workload.bicep) configures the Functions API with its Cosmos connection string, `release-notes` database name, `releases` container name, and Application Insights connection string. The application creates the database and container on first use; no manual portal configuration is needed.

1. [Configure Azure OIDC for GitHub Actions](configure-azure-oidc-for-github-actions.md)
2. [Provision with Azure CLI](provision-with-azure-cli.md)
3. [Deploy with GitHub Actions](deploy-with-github-actions.md)
4. [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md)