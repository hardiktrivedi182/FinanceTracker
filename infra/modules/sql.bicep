// ---------------------------------------------------------------------------
// Module: Azure SQL Server + Database
// Dev  → Basic tier (5 DTU, 2 GB max) ≈ $5/month
// Prod → Standard S0 (10 DTU)
// ---------------------------------------------------------------------------

@description('Name of the Azure SQL Server.')
param serverName string

@description('Name of the SQL Database.')
param databaseName string

@description('Azure region.')
param location string

@allowed(['dev', 'prod'])
@description('Deployment environment.')
param environmentName string

@description('Azure AD object ID for the SQL administrator.')
param adminObjectId string

@description('Azure AD login name for the SQL administrator.')
param adminLoginName string

@description('Enable private endpoint (recommended for prod).')
param enablePrivateEndpoint bool = environmentName == 'prod'

// ── SQL Server ──────────────────────────────────────────────────────────────

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: adminLoginName
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// ── SQL Database ────────────────────────────────────────────────────────────

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: environmentName == 'prod' ? 'Standard' : 'Basic'
    tier: environmentName == 'prod' ? 'Standard' : 'Basic'
    capacity: environmentName == 'prod' ? 10 : 5
  }
  properties: {
    maxSizeBytes: environmentName == 'prod' ? 268435456000 : 2147483648 // 250 GB prod, 2 GB dev
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

@description('The name of the SQL Server.')
output serverName string = sqlServer.name

@description('The name of the SQL Database.')
output databaseName string = sqlDatabase.name

@description('The fully qualified domain name of the SQL Server.')
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
