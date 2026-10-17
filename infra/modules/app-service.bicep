// ---------------------------------------------------------------------------
// Module: App Service Plan + Web App
// Dev  → B1 (Basic) tier
// Prod → P0v3 (Premium V3) — supports managed identity + deployment slots
// ---------------------------------------------------------------------------

@description('Name of the App Service Plan.')
param appServicePlanName string

@description('Name of the Web App.')
param webAppName string

@description('Azure region.')
param location string

@allowed(['dev', 'prod'])
@description('Deployment environment.')
param environmentName string

// ── App Service Plan ────────────────────────────────────────────────────────

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  properties: {
    reserved: true // Required for Linux
  }
  sku: {
    name: environmentName == 'prod' ? 'P0v3' : 'B1'
    tier: environmentName == 'prod' ? 'PremiumV3' : 'Basic'
  }
}

// ── Web App ─────────────────────────────────────────────────────────────────

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      alwaysOn: environmentName == 'prod'
      appSettings: [
        // ── Connection strings via Key Vault references ──
        // These placeholders show the pattern; actual Key Vault URI is set at
        // deployment time once the Key Vault module is added.
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: '@Microsoft.KeyVault(SecretUri=https://<keyvault-name>.vault.azure.net/secrets/SqlConnectionString/)'
        }
        {
          name: 'Storage__ConnectionString'
          value: '@Microsoft.KeyVault(SecretUri=https://<keyvault-name>.vault.azure.net/secrets/StorageConnectionString/)'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
      ]
    }
  }
  tags: {
    environment: environmentName
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────

@description('Name of the Web App.')
output webAppName string = webApp.name

@description('Default hostname of the Web App.')
output webAppDefaultHostName string = webApp.properties.defaultHostName

@description('Principal ID of the system-assigned managed identity.')
output systemIdentityPrincipalId string = webApp.identity.principalId

@description('Resource ID of the App Service Plan.')
output appServicePlanId string = appServicePlan.id
