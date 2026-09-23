# Design

## Context

See [proposal.md](proposal.md) for motivation and [the deployment capability spec](specs/azure-workload-deployment/spec.md) for required behavior. The repository already deploys `src/Client` and `src/Api` with `Azure/static-web-apps-deploy@v1`, but requires a manually created Static Web App and a repository secret containing its deployment token. The Functions API reads `Cosmos:ConnectionString`, `Cosmos:Database`, and `Cosmos:Container`; its data store creates the database and container when it first handles data.

## Goals / Non-Goals

**Goals:**
- Make the resource group, Static Web App, Cosmos DB account, and Static Web App application settings reproducible from source control.
- Keep ongoing Azure cost at the lowest supported tiers compatible with the current architecture.
- Use GitHub Actions OpenID Connect authentication and retrieve the deployment token during each workflow run rather than storing that token as a repository secret.
- Give maintainers one README path for Azure bootstrap, GitHub configuration, deployment, and Azure DevOps endpoint configuration.

**Non-Goals:**
- Rework the client, Functions API, or Cosmos data-access behavior.
- Migrate persisted release-note data from an existing manually provisioned account.
- Create or manage the Microsoft Entra application and GitHub federated credential in Bicep, because those identities must be bootstrapped with subscription and repository-specific authority.
- Provide production high-availability, private networking, backup, or cost-monitoring policies beyond the requested lowest-cost deployment.

## Decisions

### Use a subscription-scope Bicep entry point with a resource-group module

The entry point will create `rg-azure-devops-release-notes` in the configured Azure region, then invoke a resource-group-scoped module for the workload resources. This lets a first deployment create the fixed resource group while keeping workload resources scoped where they live.

Resource names that must be globally unique, including the Static Web App and Cosmos account, will derive from a short parameterized suffix. The deployment will output the Static Web App name and default hostname so the workflow and README do not reconstruct resource identifiers.

Alternative considered: a resource-group-only Bicep file. It cannot create the requested resource group, leaving an imperative bootstrap step outside the declared deployment.

### Provision only resources required by the existing application and use economical tiers

The workload module will create a Static Web App with the Free SKU and a Cosmos DB SQL API account configured with `enableFreeTier: true` and serverless capacity. It will set the Static Web App API application settings from the Cosmos connection string and fixed application defaults: `Cosmos:Database=release-notes` and `Cosmos:Container=releases`.

The Bicep deployment will not declare the database or container. The running API deliberately creates them using those names and `/projectId` as its partition key; adding a second provisioning lifecycle risks incompatible declarations and conflict with that initialization behavior.

Alternative considered: dedicated provisioned Cosmos throughput and a paid Static Web Apps plan. They offer predictable capacity and more features, but violate the requested free or lowest-tier posture for this workload.

### Authenticate GitHub Actions through Azure OIDC and fetch the deploy token at runtime

The workflow will grant `id-token: write`, use `azure/login` with repository secrets or variables for tenant, subscription, and client identity, and run an Azure CLI subscription deployment. A subsequent Azure CLI command will retrieve the target Static Web App deployment token and expose it as a masked step output or environment value for `Azure/static-web-apps-deploy@v1`.

This removes `AZURE_STATIC_WEB_APPS_API_TOKEN` as a long-lived repository secret. The GitHub environment must bootstrap an Entra application/service principal with a federated credential restricted to this repository and appropriate subscription permissions; Bicep cannot safely create that relationship without preexisting identity authority.

Alternative considered: retain the Static Web Apps deployment token as a GitHub secret. It is simpler but introduces token rotation and secret-management work that OIDC already avoids for infrastructure access.

### Keep application deployment behavior and tests in the existing workflow

Infrastructure deployment will occur after checkout and Azure authentication, while the existing .NET test step remains a required gate before `Azure/static-web-apps-deploy@v1` uploads the client and API. Pull-request preview behavior will remain subject to Azure credentials being available; the close-preview job will use the same runtime token acquisition method if it continues to close preview environments.

Alternative considered: split infrastructure and application deployment into separate workflows. That creates ordering and handoff complexity without a current need for independently scheduled infrastructure releases.

### Replace manual portal instructions with reproducible setup guidance

The README will name the Bicep files, fixed resource group and region, generated-name parameter, Azure CLI deployment command, OIDC GitHub variables/secrets and required role assignment. It will also explain how to use the Bicep output hostname to update `releaseNotesApiUrl` in the Azure DevOps pipeline.

## Risks / Trade-offs

- [Free Static Web Apps capacity or features may not support future workload demands] -> Document the Free SKU as intentional and keep the Bicep SKU parameter isolated for later elevation.
- [Cosmos free tier is limited to one account per subscription and serverless pricing remains consumption-based] -> Document the limitation and expose account naming and deployment outputs so maintainers can select a suitable subscription.
- [OIDC setup is not fully self-bootstrapping] -> Provide exact README prerequisites, required GitHub values, and minimum Azure role assignment before the first workflow run.
- [A GitHub pull request from an untrusted fork cannot access Azure credentials] -> Let the authenticated deployment step fail safely; do not expose credentials to forked workflows.
- [Existing manually provisioned environments use different names or data] -> Keep this change as a new reproducible environment path and document migration as out of scope.

## Migration Plan

1. Add the Bicep entry point and workload module, including outputs and deployment parameter documentation.
2. Bootstrap the Entra application/service principal and GitHub federated credential, then grant the deployment identity the documented subscription scope permissions.
3. Configure the required GitHub Azure identity values and run the workflow or documented Azure CLI deployment to create the environment.
4. Update the workflow to provision first, retrieve the deploy token, run tests, and publish through the existing Static Web Apps action.
5. Update `releaseNotesApiUrl` in the Azure DevOps pipeline using the deployed hostname and verify a release-notes compilation request persists data.

To roll back a failed application deployment, rerun the workflow from the last known-good commit. To roll back a failed infrastructure update, redeploy the previous Bicep revision; resource deletion is an explicit operator action and is not performed automatically.