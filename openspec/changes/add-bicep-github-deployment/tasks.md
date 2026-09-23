# Tasks

## 1. Infrastructure as Code

- [x] 1.1 Create a subscription-scope Bicep entry point that creates `rg-azure-devops-release-notes` in the configured Azure region, accepts a globally unique naming suffix, invokes the workload module, and outputs the Static Web App name and hostname; verify with `az bicep build`.
- [x] 1.2 Create the resource-group workload Bicep module with a Free Azure Static Web App and a SQL API Cosmos DB account that enables free tier and serverless capacity; verify a Bicep build resolves the resource API versions and dependency graph.
- [x] 1.3 Configure Static Web App API settings from the Cosmos connection string plus `Cosmos:Database=release-notes` and `Cosmos:Container=releases`; verify the compiled template contains the three expected application settings without exposing the connection string as an output.
- [x] 1.4 Add a sample parameter file or documented parameter contract for the unique resource-name suffix; verify a what-if command can target a subscription without manually creating a resource group.

## 2. GitHub Deployment Workflow

- [x] 2.1 Update `.github/workflows/azure-static-web-apps.yml` with least-privilege workflow permissions and `azure/login` OIDC authentication using documented repository Azure identity configuration; verify workflow YAML parses and authentication precedes Azure CLI use.
- [x] 2.2 Add a subscription-scope Bicep deployment step that supplies the naming suffix and captures the Static Web App resource name from deployment outputs; verify the workflow deploy command targets the Bicep entry point and does not depend on a pre-existing resource group.
- [x] 2.3 Retrieve and mask the provisioned Static Web App deployment token at workflow runtime, pass it to both upload and pull-request preview-close actions, and remove the long-lived static deployment-token secret dependency; verify every `Azure/static-web-apps-deploy@v1` invocation receives the runtime token.
- [x] 2.4 Preserve the existing Release .NET test gate before application upload and ensure Azure authentication or infrastructure deployment failure prevents publishing; verify the workflow ordering through YAML review and run `dotnet test --configuration Release`.

## 3. Deployment Documentation

- [x] 3.1 Replace README manual Azure resource creation instructions with the Bicep layout, resource group `rg-azure-devops-release-notes`, configured Azure location, Free Static Web Apps SKU, and Cosmos free-tier serverless limitations; verify the deployment section contains each value.
- [x] 3.2 Document Entra application/service-principal bootstrap, repository-scoped GitHub OIDC federated credential, required Azure role assignment, and repository Azure configuration values; verify a maintainer can identify every prerequisite without a Static Web Apps deployment-token secret.
- [x] 3.3 Document local Azure CLI provisioning and GitHub Actions deployment paths, including how to use the Bicep output hostname to set `releaseNotesApiUrl` in `pipelines/azure-pipelines.yml`; verify the endpoint example retains the `/api/releases/compile` route.

## 4. Validation

- [ ] 4.1 Run `az bicep build` for the deployment entry point and a subscription-level `az deployment sub what-if` with a valid suffix; verify the what-if proposes the named resource group, Static Web App, and Cosmos DB resources.
- [x] 4.2 Run `dotnet test --configuration Release` and inspect the updated GitHub Actions workflow with a YAML parser or GitHub Actions validation; verify existing tests pass and the workflow has no syntax errors.