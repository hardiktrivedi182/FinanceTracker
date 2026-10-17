// ---------------------------------------------------------------------------
// FinanceTracker — Main orchestrator template
// Composes all infrastructure modules with environment-aware configuration.
// ---------------------------------------------------------------------------

targetScope = 'resourceGroup'

// ── Parameters ──────────────────────────────────────────────────────────────

@allowed(['dev', 'prod'])
@description('Deployment environment (dev or prod).')
param environmentName string

@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Base application name used for resource naming.')
param appName string = 'financetracker'

@description('Azure AD object ID of the SQL administrator.')
param sqlAdminObjectId string

@description('Login name for the Azure AD SQL administrator.')
param sqlAdminLoginName string = '${appName}-sql-admin'

// ── Derived Names ───────────────────────────────────────────────────────────

var baseName = '${appName}-${environmentName}'
// Storage account names: lowercase, no hyphens, max 24 chars
var storageAccountName = take(replace('${appName}${environmentName}st', '-', ''), 24)

// ── Modules ─────────────────────────────────────────────────────────────────

module identity 'modules/identity.bicep' = {
  name: 'identity-${baseName}'
  params: {
    identityName: '${baseName}-id'
    location: location
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql-${baseName}'
  params: {
    serverName: '${baseName}-sql'
    databaseName: '${baseName}-db'
    location: location
    environmentName: environmentName
    adminObjectId: sqlAdminObjectId
    adminLoginName: sqlAdminLoginName
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage-${baseName}'
  params: {
    storageAccountName: storageAccountName
    location: location
    environmentName: environmentName
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'appservice-${baseName}'
  params: {
    appServicePlanName: '${baseName}-plan'
    webAppName: '${baseName}-app'
    location: location
    environmentName: environmentName
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

@description('Name of the deployed SQL Server.')
output sqlServerName string = sql.outputs.serverName

@description('Name of the deployed SQL Database.')
output sqlDatabaseName string = sql.outputs.databaseName

@description('Name of the deployed Storage Account.')
output storageAccountName string = storage.outputs.storageAccountName

@description('Name of the deployed Web App.')
output webAppName string = appService.outputs.webAppName

@description('Default hostname of the deployed Web App.')
output webAppHostName string = appService.outputs.webAppDefaultHostName

@description('Principal ID of the user-assigned managed identity.')
output identityPrincipalId string = identity.outputs.principalId

@description('Client ID of the user-assigned managed identity.')
output identityClientId string = identity.outputs.clientId
