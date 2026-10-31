# 1. Configure Azure OIDC for GitHub Actions

Create or select an Entra deployment application/service principal. Add a GitHub Actions federated credential restricted to this repository and `refs/heads/main`, and grant `Contributor` at the subscription scope so it can create the resource group and workload. Keep directory administration separate from Azure RBAC. PR validation does not log in or deploy.

## API registration and callers

Before enabling deployed checks, a directory administrator must prepare a separate single-tenant API app registration:

1. Record its application/client ID, configure an Application ID URI such as `api://<API-client-id>`, and request v2 access tokens (`requestedAccessTokenVersion=2`). Bicep configures the matching tenant issuer and accepted audience.
2. Define an application permission/app role such as `ReleaseNotes.Compile` for application callers. Require assignment on the API enterprise application so unassigned identities cannot obtain roleless API access. Grant the deployment service principal that API permission and administrator consent.
3. Give each Azure DevOps pipeline calling identity equivalent API access and its own workload federation or managed-identity token flow. Inbound permissions do not grant Azure DevOps project read access to the server.
4. Verify each caller can request a token for the API, not Azure Resource Manager. For an authorized Azure CLI workload session: `az account get-access-token --resource "$RELEASE_NOTES_AUTH_AUDIENCE" --query accessToken --output tsv`. Do not log the resulting token; use `Authorization: Bearer <token>` on `POST /api/releases/compile`.

These are Entra directory prerequisites, not side effects of Bicep. No API client secret is needed for bearer-token-only automated callers. Easy Auth allows anonymous platform traffic for public routes while the application protects compilation. Its trusted identity headers must never be treated as credentials on a non-Azure host.

In **Settings > Secrets and variables > Actions > Variables**, configure these repository variables:

- `AZURE_CLIENT_ID`: Application (client) ID of the Entra application.
- `AZURE_TENANT_ID`: Microsoft Entra tenant ID.
- `AZURE_SUBSCRIPTION_ID`: Subscription that will contain the deployment.
- `AZURE_LOCATION`: Azure region for the deployment.
- `AZURE_RESOURCE_SUFFIX`: A globally unique, 3-24 character lowercase alphanumeric suffix, such as `contoso123`, used in web app, plan, and existing Cosmos names.
- `RELEASE_NOTES_AUTH_CLIENT_ID`: The separate API registration's application/client ID.
- `RELEASE_NOTES_AUTH_AUDIENCE`: The API Application ID URI; the accepted audiences also include that API registration's client ID for v2 tokens.
- `AZURE_DEVOPS_ORGANIZATION`, `AZURE_DEVOPS_PROJECT`, `AZURE_DEVOPS_PROJECT_ID`, `AZURE_DEVOPS_REPOSITORY_ID`, `AZURE_DEVOPS_BUILD_ID`: The Azure DevOps test build that the post-deploy integration tests compile. [Set up the Azure DevOps test project](set-up-the-azure-devops-test-project.md) writes these keys to `.env`.

Rather than entering these variables by hand, put them in `.env` and run `./scripts/Sync-Environment.ps1` (see [Environment configuration](environment-configuration.md)).

The workflow mints and masks short-lived API tokens using the federated deployment identity. Never persist them in repository configuration or client assets. SWA deployment tokens and Function keys are no longer used. The runtime web app's system identity still needs its separate Azure DevOps Basic/Readers grant; see [test-project setup](set-up-the-azure-devops-test-project.md).