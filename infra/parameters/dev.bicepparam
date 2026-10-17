using '../main.bicep'

// ── Dev Environment Parameters ──────────────────────────────────────────────
// Uses the cheapest Azure tiers suitable for development/testing.

param environmentName = 'dev'
param appName = 'financetracker'
param sqlAdminObjectId = '<azure-ad-admin-object-id>'
param sqlAdminLoginName = 'financetracker-sql-admin'
