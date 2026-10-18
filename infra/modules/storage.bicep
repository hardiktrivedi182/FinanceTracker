// ---------------------------------------------------------------------------
// Module: Azure Storage Account
// LRS redundancy, Hot tier, HTTPS only, TLS 1.2, no public blob access.
// Creates a 'statements' blob container with private access.
// ---------------------------------------------------------------------------

@description('Name of the storage account (lowercase, no hyphens, max 24 chars).')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Azure region.')
param location string

@allowed(['dev', 'prod'])
@description('Deployment environment.')
param environmentName string

@description('Optional Managed Identity Principal ID to grant Storage Blob Data Contributor.')
param managedIdentityPrincipalId string = ''

// ── Storage Account ─────────────────────────────────────────────────────────

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: environmentName == 'dev' // Prod should use managed identity only
  }
  tags: {
    environment: environmentName
  }
}

// ── Blob Services & Container ───────────────────────────────────────────────

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

resource statementsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: 'statements'
  properties: {
    publicAccess: 'None'
  }
}

// ── Role Assignment: Storage Blob Data Contributor ─────────────────────────
// Grants the managed identity permission to read and write statement blobs.
var storageBlobDataContributorRoleDefinitionId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')

resource blobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(managedIdentityPrincipalId)) {
  name: guid(storageAccount.id, managedIdentityPrincipalId, storageBlobDataContributorRoleDefinitionId)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleDefinitionId
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

@description('Name of the storage account.')
output storageAccountName string = storageAccount.name

@description('Primary blob endpoint.')
output primaryBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('Resource ID of the storage account.')
output storageAccountId string = storageAccount.id
