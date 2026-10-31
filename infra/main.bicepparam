using './main.bicep'

// Azure region for the workload resources.
param location = 'eastus'

// Replace with a globally unique 3-24 character lowercase alphanumeric suffix.
param resourceNameSuffix = 'example123'

param authTenantId = readEnvironmentVariable('AZURE_TENANT_ID')
param authClientId = readEnvironmentVariable('RELEASE_NOTES_AUTH_CLIENT_ID')
param authAudience = readEnvironmentVariable('RELEASE_NOTES_AUTH_AUDIENCE')
param appServicePlanSku = 'B1'
