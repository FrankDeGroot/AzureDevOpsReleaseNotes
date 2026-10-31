targetScope = 'subscription'

@description('Suffix used to make globally unique Azure resource names. Use 3-24 lowercase alphanumeric characters.')
@minLength(3)
@maxLength(24)
param resourceNameSuffix string

@description('Azure region for the release-notes workload.')
param location string

@description('Microsoft Entra tenant for the API.')
@minLength(36)
@maxLength(36)
param authTenantId string

@description('Client ID of the API application registration.')
@minLength(36)
@maxLength(36)
param authClientId string

@description('Audience accepted by the API Easy Auth provider.')
@minLength(1)
param authAudience string

@description('Dedicated Linux App Service plan SKU.')
@allowed(['B1', 'B2', 'B3', 'S1', 'S2', 'S3'])
param appServicePlanSku string = 'B1'

var resourceGroupName = 'rg-azure-devops-release-notes'

resource workloadResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module workload 'workload.bicep' = {
  scope: workloadResourceGroup
  params: {
    location: location
    resourceNameSuffix: resourceNameSuffix
    authTenantId: authTenantId
    authClientId: authClientId
    authAudience: authAudience
    appServicePlanSku: appServicePlanSku
  }
}

output resourceGroupName string = workloadResourceGroup.name
output webAppName string = workload.outputs.webAppName
output webAppDefaultHostname string = workload.outputs.webAppDefaultHostname
output appInsightsName string = workload.outputs.appInsightsName
output appInsightsConnectionString string = workload.outputs.appInsightsConnectionString
