// app-service.bicep
// Deploys App Service Plan, App Service, and User-Assigned Managed Identity

@description('Location for all resources')
param location string = 'uksouth'

@description('Unique suffix derived from resource group')
var uniqueSuffix = uniqueString(resourceGroup().id)

// Static naming for managed identity using a fixed pattern (utcNow() not allowed in variables)
var appServicePlanName = 'asp-expensemgmt-${uniqueSuffix}'
var appServiceName = 'app-expensemgmt-${uniqueSuffix}'
var managedIdentityName = 'mid-AppModAssist-01-01-00'

// User-Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// App Service Plan - Standard S1 to avoid cold start
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  kind: 'app'
}

// App Service
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appServiceName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
      ]
      alwaysOn: true
    }
  }
}

// Outputs
output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
