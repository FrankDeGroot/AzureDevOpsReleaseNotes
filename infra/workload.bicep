@description('Azure region for the workload resources.')
param location string

@description('Suffix used to make globally unique Azure resource names. Use 3-24 lowercase alphanumeric characters.')
@minLength(3)
@maxLength(24)
param resourceNameSuffix string

@description('The .NET runtime version for the Linux web app.')
param dotnetVersion string = '10.0'

@description('Microsoft Entra tenant hosting the API registration.')
@minLength(36)
@maxLength(36)
param authTenantId string

@description('Application client ID of the API registration, not the deployment identity.')
@minLength(36)
@maxLength(36)
param authClientId string

@description('API audience accepted by Easy Auth.')
@minLength(1)
param authAudience string

@description('Dedicated App Service plan SKU. B1 incurs a standing compute charge.')
@allowed(['B1', 'B2', 'B3', 'S1', 'S2', 'S3'])
param appServicePlanSku string = 'B1'

var cosmosAccountName = 'cosmosreleasenotes${resourceNameSuffix}'
var hostingPlanName = 'asp-release-notes-web-${resourceNameSuffix}'
var webAppName = 'app-release-notes-${resourceNameSuffix}'
var logAnalyticsWorkspaceName = 'log-release-notes-${resourceNameSuffix}'
var appInsightsName = 'appi-release-notes-${resourceNameSuffix}'

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

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  sku: {
    name: appServicePlanSku
    tier: startsWith(appServicePlanSku, 'B') ? 'Basic' : 'Standard'
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|${dotnetVersion}'
      appCommandLine: 'dotnet Api.dll'
      alwaysOn: true
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'EasyAuth__TenantId'
          value: authTenantId
        }
        {
          name: 'EasyAuth__ClientId'
          value: authClientId
        }
        {
          name: 'EasyAuth__Audience'
          value: authAudience
        }
        {
          name: 'Cosmos__ConnectionString'
          value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
        }
        {
          name: 'Cosmos__Database'
          value: 'release-notes'
        }
        {
          name: 'Cosmos__Container'
          value: 'releases'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

resource authentication 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: webApp
  name: 'authsettingsV2'
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      requireAuthentication: false
      unauthenticatedClientAction: 'AllowAnonymous'
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: authClientId
          openIdIssuer: '${environment().authentication.loginEndpoint}${authTenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [authAudience, authClientId]
        }
      }
    }
    httpSettings: {
      requireHttps: true
    }
  }
}

output webAppName string = webApp.name
output webAppDefaultHostname string = webApp.properties.defaultHostName
output appInsightsName string = appInsights.name
output appInsightsConnectionString string = appInsights.properties.ConnectionString
