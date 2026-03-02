// genai.bicep
// Deploys Azure OpenAI (Sweden Central) and AI Search (UK South)
// Uses existing managed identity - does NOT create a new one

@description('Location for Azure OpenAI - MUST be swedencentral for quota availability')
param openAILocation string = 'swedencentral'

@description('Location for AI Search')
param searchLocation string = 'uksouth'

@description('Principal ID of the managed identity to grant OpenAI and Search access')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)

// Names must be lowercase to avoid Azure OpenAI subdomain validation errors
var openAIName = 'aoai-expensemgmt-${uniqueSuffix}'
var searchName = 'srch-expensemgmt-${uniqueSuffix}'

// Role definition IDs
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
var searchIndexDataReaderRoleId = '1407120a-92aa-4202-b7e9-c0e197c71c8f'

// Azure OpenAI - must be in swedencentral for GPT-4o quota
resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

// GPT-4o deployment - Standard SKU, capacity 8
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: 'gpt-4o'
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
  }
}

// AI Search - S0 SKU in uksouth
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: searchLocation
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    publicNetworkAccess: 'enabled'
  }
}

// Grant managed identity "Cognitive Services OpenAI User" role on OpenAI resource
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRoleId)
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Grant managed identity "Search Index Data Reader" role on AI Search
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataReaderRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataReaderRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output openAIEndpoint string = openAI.properties.endpoint
output openAIModelName string = gpt4oDeployment.name
output openAIName string = openAI.name
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
