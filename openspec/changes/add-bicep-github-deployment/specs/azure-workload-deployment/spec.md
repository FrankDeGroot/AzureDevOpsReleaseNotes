# Spec Delta

## Purpose

Provide repeatable, economical Azure provisioning and GitHub-based deployment for the complete release-notes application workload.

## ADDED Requirements

### Requirement: Declarative Azure workload provisioning
The repository SHALL provide a Bicep deployment that creates resource group `rg-azure-devops-release-notes` in Germany West Central and provisions an Azure Static Web App with an integrated API and an Azure Cosmos DB account for release-note storage. The deployment SHALL configure the Static Web App with the Cosmos connection string and the `release-notes` database and `releases` container names expected by the application.

The deployment SHALL select free or lowest-cost supported service tiers. The Cosmos DB account SHALL enable the account free tier and use serverless capacity.

#### Scenario: Provision a new environment
- **WHEN** an authorized operator deploys the Bicep definition to an Azure subscription
- **THEN** `rg-azure-devops-release-notes` is created in Germany West Central with an Azure Static Web App and a Cosmos DB account
- **AND** the Static Web App API receives the Cosmos configuration required to persist release notes

#### Scenario: Reapply the deployment
- **WHEN** an authorized operator deploys the same Bicep definition after the environment already exists
- **THEN** the deployment updates the declared resources without requiring manual resource creation
- **AND** the deployment preserves the requested resource group name and region

### Requirement: GitHub Actions infrastructure and application deployment
The GitHub Actions workflow SHALL authenticate to Azure using repository-configured federated credentials, deploy the Bicep infrastructure before application deployment, and use the provisioned Static Web App deployment credential for the existing client and API deployment.

The workflow SHALL continue to run tests before publishing application artifacts and SHALL fail before application publishing when infrastructure provisioning or Azure authentication fails.

#### Scenario: Deploy from the main branch
- **WHEN** a push to `main` starts the deployment workflow with valid Azure federated credentials
- **THEN** the workflow provisions or updates the declared Azure resources
- **AND** deploys the Blazor client and integrated Functions API to the provisioned Static Web App after tests pass

#### Scenario: Azure access is not configured
- **WHEN** the deployment workflow runs without valid Azure federated credentials
- **THEN** infrastructure provisioning fails before application publishing
- **AND** the workflow output identifies the failed Azure authentication or authorization step

### Requirement: Deployment setup documentation
The README SHALL document the Bicep deployment layout, the Azure subscription and GitHub federated-identity prerequisites, the repository configuration values required by the workflow, and how to identify the deployed Static Web App endpoint for the Azure DevOps release-notes pipeline.

The documentation SHALL state that the deployment targets resource group `rg-azure-devops-release-notes` in Germany West Central and identify the cost-sensitive service tier selections.

#### Scenario: Configure a new repository deployment
- **WHEN** a maintainer follows the README for a new repository deployment
- **THEN** they can configure the required GitHub Azure credentials and run the infrastructure and application deployment without separately creating Azure resources in the portal

#### Scenario: Connect the Azure DevOps pipeline
- **WHEN** a maintainer completes a successful deployment
- **THEN** the README directs them to configure the Azure DevOps pipeline API endpoint using the deployed Static Web App URL