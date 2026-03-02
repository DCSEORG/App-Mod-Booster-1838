// azure-sql.bicep
// Deploys Azure SQL Server and Northwind database with Azure AD-only authentication

@description('Location for all resources')
param location string = 'uksouth'

@description('Object ID of the Azure AD admin for the SQL Server')
param adminObjectId string

@description('Login name (UPN) of the Azure AD admin')
param adminLogin string

@description('Principal ID of the managed identity to grant db access')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = 'sql-expensemgmt-${uniqueSuffix}'
var databaseName = 'Northwind'

// Azure SQL Server with Azure AD-only authentication
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  name: sqlServerName
  location: location
  properties: {
    // Disable SQL authentication - Azure AD only per MCAPS policy SFI-ID4.2.2
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      tenantId: tenant().tenantId
    }
  }
}

// Northwind database - Basic tier for development
resource database 'Microsoft.Sql/servers/databases@2021-11-01' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

// Allow Azure services firewall rule
resource firewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2021-11-01' = {
  parent: sqlServer
  name: 'AllowAllAzureIPs'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Outputs
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output sqlServerName string = sqlServer.name
