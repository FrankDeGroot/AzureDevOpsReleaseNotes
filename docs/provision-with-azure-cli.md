# 2. Provision with Azure CLI

Only run these provisioning commands when authorized. Prepare the [Entra prerequisites](configure-azure-oidc-for-github-actions.md), set the tenant/API values, select the intended subscription, and confirm regional .NET 10 and B1 availability/cost. From the repository root:

```bash
az login
az account set --subscription "<subscription-id>"
az deployment sub create \
	--name release-notes-local \
	--location "<azure-region>" \
	--template-file infra/main.bicep \
	--parameters location="<azure-region>" resourceNameSuffix="<lowercase-alphanumeric-suffix>" \
	  authTenantId="$AZURE_TENANT_ID" authClientId="$RELEASE_NOTES_AUTH_CLIENT_ID" \
	  authAudience="$RELEASE_NOTES_AUTH_AUDIENCE" appServicePlanSku=B1
```

This creates or updates the group/workload incrementally. It does not delete legacy hosting resources omitted from the template. Never use complete-mode deployment or group deletion for this migration. [infra/main.bicepparam](../infra/main.bicepparam) reads required tenant/API environment values; replace its example suffix and region before using it. Missing values intentionally fail compilation rather than disabling authentication.

Compile without deploying with `az bicep build --file infra/main.bicep --stdout` and, with the required environment variables loaded, `az bicep build-params --file infra/main.bicepparam --stdout`. Outputs include `webAppName`, `webAppDefaultHostname`, `appInsightsName`, and `appInsightsConnectionString`. Keep Cosmos resource names and settings unchanged when migrating an existing environment.