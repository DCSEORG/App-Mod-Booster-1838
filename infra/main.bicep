// main.bicep
// Orchestrates deployment of App Service, Azure SQL, and optionally GenAI resources

@description('Primary location for most resources')
param location string = 'uksouth'

@description('Object ID of the Azure AD admin for SQL Server')
param adminObjectId string

@description('Login (UPN) of the Azure AD admin for SQL Server')
param adminLogin string

@description('Whether to deploy GenAI resources (Azure OpenAI + AI Search)')
param deployGenAI bool = false

// App Service + Managed Identity module
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
  }
}

// Azure SQL module
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// GenAI module - conditionally deployed
module genAI 'genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceUrl string = appService.outputs.appServiceUrl
output appServiceName string = appService.outputs.appServiceName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityId string = appService.outputs.managedIdentityId
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName
output sqlServerName string = azureSql.outputs.sqlServerName
output openAIEndpoint string = deployGenAI ? genAI.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genAI.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genAI.outputs.searchEndpoint : ''
