@description('Azure region for the workload resources.')
param location string

@description('Suffix used to make globally unique Azure resource names. Use 3-24 lowercase alphanumeric characters.')
@minLength(3)
@maxLength(24)
param resourceNameSuffix string

var staticWebAppName = 'swa-release-notes-${resourceNameSuffix}'
var cosmosAccountName = 'cosmosreleasenotes${resourceNameSuffix}'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-08-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    enableAutomaticFailover: false
    enableFreeTier: true
    enableMultipleWriteLocations: false
    publicNetworkAccess: 'Enabled'
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

resource staticWebAppSettings 'Microsoft.Web/staticSites/config@2023-12-01' = {
  parent: staticWebApp
  name: 'appsettings'
  properties: {
    'Cosmos__ConnectionString': cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
    'Cosmos__Database': 'release-notes'
    'Cosmos__Container': 'releases'
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostname string = staticWebApp.properties.defaultHostname
