# Spec Delta

## MODIFIED Requirements

### Requirement: Declarative Azure workload provisioning
The repository SHALL provide Bicep provisioning for `rg-azure-devops-release-notes` in the configured region with one Linux App Service web app, a supported dedicated plan, and Cosmos DB. The API SHALL receive Cosmos connection, database, and container settings. Services SHALL use the lowest-cost supported tiers; existing Cosmos free-tier and serverless settings and data SHALL be preserved.

#### Scenario: Provision a new environment
- **WHEN** an authorized operator deploys the Bicep definition with the required Azure and Entra configuration
- **THEN** the configured region contains the resource group, one web app and dedicated plan, and Cosmos DB
- **AND** the web app receives Cosmos configuration, a system-assigned managed identity, and Application Insights configuration
- **AND** no Static Web App, Function App, or Functions-only storage account is provisioned

#### Scenario: Reapply the deployment
- **WHEN** an authorized operator reapplies the Bicep definition
- **THEN** the deployment updates the declared resources without manual resource creation and preserves the resource group and region
- **AND** the existing Cosmos account and release-note records are not replaced or deleted
- **AND** existing legacy hosting resources are not assumed to be deleted by their removal from the template

### Requirement: GitHub Actions infrastructure and application deployment
GitHub Actions SHALL authenticate to Azure with federated credentials, provision infrastructure before application deployment, and deploy one combined application artifact to App Service. Tests SHALL run before artifact publishing. Azure authentication or infrastructure failures SHALL prevent application publishing. Post-deployment API and browser checks SHALL target the same web app origin.

#### Scenario: Deploy from the main branch
- **WHEN** a push to `main` starts the deployment workflow with valid configuration and tests pass
- **THEN** the workflow provisions or updates App Service and deploys the combined client/API artifact
- **AND** it runs integration tests using an Entra bearer token for compilation and live browser tests against the web app URL
- **AND** it does not retrieve a SWA deployment token or Function key or perform separate Functions and SWA deployments

#### Scenario: Azure access is not configured
- **WHEN** the workflow runs without valid Azure federated credentials
- **THEN** provisioning fails before application publishing
- **AND** workflow output identifies the failed authentication or authorization step

### Requirement: Deployment setup documentation
The README and linked guides SHALL describe the App Service Bicep layout, Azure federation, Entra Easy Auth prerequisites, caller token setup, required repository configuration, and the deployed URL. They SHALL identify `rg-azure-devops-release-notes`, the configured region, chosen service tiers and standing compute cost, and the replacement for Function-key callers.

#### Scenario: Configure a new repository deployment
- **WHEN** a maintainer follows the deployment setup documentation
- **THEN** they can configure Entra prerequisites and GitHub federation and deploy the Azure workload without manually creating hosting resources in the portal
- **AND** the documentation distinguishes Entra registration/consent setup from Azure resource provisioning

#### Scenario: Connect the Azure DevOps pipeline
- **WHEN** a maintainer completes deployment
- **THEN** the documentation directs them to use the App Service `/api/releases/compile` URL with an Entra access token for the configured API audience
- **AND** it explains how to grant the web app's outbound managed identity access to the Azure DevOps project

## ADDED Requirements

### Requirement: Easy Auth permits public reads and validates compile identities
The Azure web app SHALL enable Microsoft Entra Easy Auth with a configured tenant and accepted API audience while allowing anonymous traffic to reach public routes. Compilation SHALL require the validated identity as specified by `release-notes-compiler`. The Azure deployment SHALL use a non-Development environment.

#### Scenario: Public routes remain accessible
- **WHEN** an unauthenticated caller loads the viewer or calls release list, release detail, or health routes
- **THEN** authentication does not redirect the caller to sign-in or deny the public route
- **AND** the route returns its normal application response

#### Scenario: Invalid authentication
- **WHEN** a compile caller presents an expired token or a token for the wrong tenant or audience
- **THEN** the call is rejected without invoking compilation

### Requirement: Verified hosting cutover and retirement
The migration SHALL document and require an operator-approved cutover, a rollback window, and explicit retirement of the old SWA, Function App, and exclusively owned plan/storage resources only after verification. Cosmos DB, release records, and shared monitoring resources SHALL NOT be deleted during hosting retirement.

#### Scenario: Cutover checks fail
- **WHEN** the combined host fails API, authorization, dependency, or browser verification
- **THEN** the old hosting resources remain available and callers can remain on or return to the previous endpoint

#### Scenario: Retire legacy hosting
- **WHEN** verification succeeds, callers are migrated, and the operator ends the documented rollback window
- **THEN** the operator can explicitly remove inventoried legacy-only hosting resources and their obsolete access grants
- **AND** Cosmos DB data and resources still needed by the new app are preserved