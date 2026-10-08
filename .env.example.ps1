# Example environment. Keep real values in .env and run scripts/Sync-Environment.ps1 to generate .env.ps1.
# Load into the current PowerShell session with: . ./.env.ps1

# Microsoft Entra tenant of the GitHub Actions OIDC application.
$env:AZURE_TENANT_ID = '00000000-0000-0000-0000-000000000000'
# Application (client) ID of the GitHub Actions OIDC application.
$env:AZURE_CLIENT_ID = '00000000-0000-0000-0000-000000000000'
# Subscription that hosts the deployment.
$env:AZURE_SUBSCRIPTION_ID = '00000000-0000-0000-0000-000000000000'
# Azure region for the deployment.
$env:AZURE_LOCATION = 'westeurope'
# Globally unique 3-24 character lowercase alphanumeric resource name suffix.
$env:AZURE_RESOURCE_SUFFIX = 'contoso123'

# Azure DevOps organization name (the part after https://dev.azure.com/).
$env:AZURE_DEVOPS_ORGANIZATION = 'my-organization'
# Azure DevOps test project name.
$env:AZURE_DEVOPS_PROJECT = 'ReleaseNotesIntegration'
# Written by scripts/Initialize-AzureDevOpsTestProject.ps1.
$env:AZURE_DEVOPS_PROJECT_ID = '00000000-0000-0000-0000-000000000000'
# Written by scripts/Initialize-AzureDevOpsTestProject.ps1.
$env:AZURE_DEVOPS_REPOSITORY_ID = '00000000-0000-0000-0000-000000000000'
# Written by scripts/Initialize-AzureDevOpsTestProject.ps1.
$env:AZURE_DEVOPS_BUILD_ID = '1'

# Local only: API base URL used by the integration tests.
$env:RELEASE_NOTES_API_URL = 'http://localhost:7071/api'
# Local only: function key; leave empty for the local Functions host.
$env:FUNCTION_KEY = ''
