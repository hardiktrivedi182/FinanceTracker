using '../main.bicep'

// ── Prod Environment Parameters ─────────────────────────────────────────────
// Uses production-appropriate tiers with security hardening.

param environmentName = 'prod'
param appName = 'financetracker'
param sqlAdminObjectId = '<azure-ad-admin-object-id>'
param sqlAdminLoginName = 'financetracker-sql-admin'
