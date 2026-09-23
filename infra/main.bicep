targetScope = 'subscription'

@description('Suffix used to make globally unique Azure resource names. Use 3-24 lowercase alphanumeric characters.')
@minLength(3)
@maxLength(24)
param resourceNameSuffix string

@description('Azure region for the release-notes workload.')
param location string

var resourceGroupName = 'rg-azure-devops-release-notes'

resource workloadResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
}

module workload 'workload.bicep' = {
  name: 'releaseNotesWorkload'
  scope: workloadResourceGroup
  params: {
    location: location
    resourceNameSuffix: resourceNameSuffix
  }
}

output resourceGroupName string = workloadResourceGroup.name
output staticWebAppName string = workload.outputs.staticWebAppName
output staticWebAppDefaultHostname string = workload.outputs.staticWebAppDefaultHostname
