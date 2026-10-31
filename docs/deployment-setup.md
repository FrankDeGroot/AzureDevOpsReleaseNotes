# Deployment Setup

The Bicep entry point at [infra/main.bicep](../infra/main.bicep) creates `rg-azure-devops-release-notes` in the configured region. It provisions one Linux App Service web app and dedicated B1-default plan, preserves Cosmos DB configuration and records, and retains Application Insights with Log Analytics. B1 has a standing compute charge even when idle; check regional pricing and .NET 10 runtime/SKU availability before provisioning. `appServicePlanSku` supports B1/B2/B3 and S1/S2/S3. Cosmos free-tier eligibility is subscription-limited; existing free-tier/serverless choices are not changed by this migration.

The workload module at [infra/workload.bicep](../infra/workload.bicep) gives the web app its Cosmos connection string, `release-notes` database, `releases` container, telemetry, Production environment, and system-assigned identity. The application initializes its database/container on first use. Both the viewer and API ship in one publish output; there is no SWA link or Functions storage dependency.

Entra API registration, consent, and caller federation require directory privileges separate from Azure RBAC and must be prepared before deployment. Easy Auth accepts anonymous platform traffic, validates supplied tokens, and application authorization protects compilation only. Give the web app's outbound managed identity Azure DevOps project access before expecting deployed tests to pass. Provisioning is not permission to delete old hosts; follow the [cutover runbook](deployment.md).

1. [Configure Azure OIDC for GitHub Actions](configure-azure-oidc-for-github-actions.md)
2. [Provision with Azure CLI](provision-with-azure-cli.md)
3. [Deploy with GitHub Actions](deploy-with-github-actions.md)
4. [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md)