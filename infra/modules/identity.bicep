// ---------------------------------------------------------------------------
// Module: User-Assigned Managed Identity
// Used for app-to-resource authentication (SQL, Storage, Key Vault, etc.)
// ---------------------------------------------------------------------------

@description('Name of the user-assigned managed identity.')
param identityName string

@description('Azure region.')
param location string

// ── Managed Identity ────────────────────────────────────────────────────────

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

// ── Outputs ─────────────────────────────────────────────────────────────────

@description('Principal (object) ID of the managed identity — use for role assignments.')
output principalId string = managedIdentity.properties.principalId

@description('Client (application) ID of the managed identity — use in app configuration.')
output clientId string = managedIdentity.properties.clientId

@description('Resource ID of the managed identity.')
output identityResourceId string = managedIdentity.id
