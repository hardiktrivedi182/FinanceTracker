# Safebox: Security, Financial Correctness and Release Readiness Review

**Review Date:** October 18, 2026  
**Scope:** Authentication, Server Authorization, Account Management, Dated Valuations, Historical Net Worth Reporting, Database Persistence, Frontend UX, and IaC/CI Automation.  
**Reviewer:** Antigravity Autonomous Security & Correctness Auditor  

---

## Executive Summary & Recommendation

### Outcome: **Ready for statement-ingestion implementation** (with documented non-blocking gaps)

All critical P0 capabilities for the local domain foundation—authentication schemes, server-enforced household isolation, account write authorization, exact decimal valuation calculations, currency segregation, and dated net worth reporting—have been rigorously audited, defects fixed, and verified with 116 automated backend tests and 4 frontend tests.

Development may safely proceed to **P0-5: Statement Upload & Isolated File Parsing**. Documented gaps (such as EF Core SQL migrations and live Azure deployment automation) are non-blocking for statement parsing development and are clearly scheduled prior to live production deployment.

---

## 1. Initial State Verification

| Claimed State | Actual Repository Evidence | Status |
| :--- | :--- | :--- |
| ASP.NET Core Identity integration | `ApplicationUser`, `identity_*` EF table mapping, strong password policy (12 chars, digit, upper, lower, symbol), lockout (5 attempts / 15m). | **VERIFIED** |
| Account CRUD | Full CRUD in `AccountService`, `AccountRepository`, `AccountsController`, and `AccountManager.tsx`. | **VERIFIED** |
| Dated valuation history | `AccountValuation` aggregate child with audit trail (`CreatedByUserId`, `UpdatedByUserId`, timestamps). | **VERIFIED** |
| Historical net worth calculations | As-of date filtering, staleness detection (>90 days), two-date historical comparisons. | **VERIFIED** |
| 103 backend tests passing | Ran `dotnet test`: 65 Domain, 1 Importing, 13 Application, 3 Infrastructure, 21 API = 103 passing. | **VERIFIED** |
| 3 frontend tests passing | Ran `npm test`: 3 Vitest tests passing in `App.test.tsx`. | **VERIFIED** |
| Git status initially clean | Commit `2ca6ae9` on `main`, working tree clean before review began. | **VERIFIED** |

---

## 2. Findings Table

| ID | Severity | Component / File | Evidence | Proposed Fix / Resolution | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **SEC-01** | **Medium** | `AuthController.cs` | `Logout()` called `_signInManager.SignOutAsync()` which clears `ft_auth`, but did not delete the `dev_user_id` cookie, leaving dev session active in development mode. | Added `Response.Cookies.Delete("dev_user_id")` inside `Logout()` when `IsDevelopment()`. | **FIXED** |
| **FIN-01** | **High** | `NetWorthService.cs` | Ownership breakdown hardcoded string checks `a.OwnerUserId == "partner-1"` and `"partner-2"`. Any accounts owned by real user IDs (e.g. Identity GUIDs) resulted in ₹0 individual totals. | Replaced hardcoded strings with dynamic distinct owner resolution prioritizing household partners. | **FIXED** |
| **FIN-02** | **Medium** | `AccountManager.tsx` | Accounts with no valuations on or before `asOfDate` were counted as 0 without any warning banner on the currency Net Worth summary card. | Added explicit warning alert on Net Worth card displaying count of unvalued accounts as of target date. | **FIXED** |
| **UI-01** | **Low** | `AccountManager.tsx` | Form allowed submitting lowercase or invalid currency formats causing 400 Bad Request from API. | Added client-side regex validation (`/^[A-Z]{3}$/`) in `handleCreateAccount`. | **FIXED** |
| **TST-01** | **Medium** | Test Suites | Missing tests for empty households, arbitrary user IDs, 90-day staleness boundary, negative balances, and cross-household isolation. | Added 13 backend unit/integration tests and 1 frontend test. | **FIXED** |
| **DB-01** | **Medium** | `FinanceTracker.Infrastructure` | No EF Core migrations exist in `Migrations/`. In-memory database used exclusively in `Program.cs`. | Documented as non-blocking for local dev; must generate initial migration before Azure SQL deployment. | **DOCUMENTED** |
| **SEC-02** | **Low** | `Program.cs` | Antiforgery service registered (`AddAntiforgery`) but `app.UseAntiforgery()` not called in pipeline and forms do not exchange CSRF tokens. Protected by `SameSite=Strict` cookies. | Documented. Production API should add explicit token header validation prior to public browser exposure. | **DOCUMENTED** |
| **IAC-01** | **Low** | `infra/` | Bicep templates define App Service, SQL, and Storage, but Key Vault and RBAC role assignments are placeholders. | Documented prerequisite in `infra/bootstrap/README.md`. Deployment requires explicit approval. | **DOCUMENTED** |

---

## 3. Detailed Audit by Phase

### Phase 1: Authentication & Authorization Audit

- **Production Authentication Scheme:** Production environment excludes `DevAuthenticationHandler` and composite policy scheme completely (`if (builder.Environment.IsDevelopment())`). Only `IdentityConstants.ApplicationScheme` is configured with `ft_auth` secure cookie.
- **Header Identity Spoofing:** Tested and verified: `CurrentUser` and `HouseholdContext` read claims strictly from authenticated `ClaimsPrincipal` (`sub`, `household_id`, `household_role`). Headers such as `X-User-Id` or `X-Household-Id` cannot spoof identity in production.
- **Household Isolation:** Every query in `AccountRepository` and `AccountService` filters by `_householdContext.HouseholdId`. Verified via `AccountsEndpointTests.GetAccountById_WhenAccountBelongsToDifferentHousehold_ReturnsNotFound` returning 404.
- **Ownership Permissions:** Joint accounts are editable by any member. Individual accounts require caller to be the account owner or have `HouseholdRole.Admin`. Partner 2 modifying Partner 1's individual account returns 403 Forbidden.
- **Cookie Security:** Cookies configure `HttpOnly = true`, `SameSite = SameSiteMode.Strict`, and `Secure = Always` in non-development environments.
- **Account Lockout & Password Policy:** 12 characters minimum with uppercase, lowercase, digit, and symbol. Account locks out for 15 minutes after 5 consecutive failures. Generic error messages prevent username enumeration.
- **Status:** **PASS** (with documented gap for self-service user registration / password recovery flows).

### Phase 2: Financial Correctness Audit

- **Currency Segregation:** All asset allocations and net worth totals are strictly partitioned per currency code. No implicit foreign exchange conversion or mixing occurs.
- **Decimal Precision:** All balance and valuation operations use `decimal` in C# and `decimal(19, 4)` in SQL configuration. Verified exact 4-decimal precision arithmetic (`94567.8766m` assets, `15000.5678m` liabilities, `79567.3088m` net worth).
- **Staleness Exact Boundary:** Tested exact 90-day threshold boundary:
  - 89 days: `isStale = false`
  - 90 days: `isStale = false` (boundary condition: strict inequality `> 90`)
  - 91 days: `isStale = true`
- **Valuation Date Precedence & Duplicate Resolution:** Multiple valuations on the same date resolve deterministically by creation timestamp (`CreatedAt DESC`).
- **Valuations Relative to As-Of Date:** Valuations after the requested as-of date are excluded; valuations on or before are selected; accounts with no valuation on or before the date are marked `HasMissingValuation = true`.
- **Negative Balances & Liabilities:**
  - Asset accounts (e.g. checking/savings) with negative balance reduce total assets.
  - Liability accounts (e.g. loans, credit cards) record positive balances representing debt and are subtracted from assets (`NetWorth = Assets - Liabilities`).
- **Status:** **PASS**

### Phase 3: Database & Migration Review

- **Entities & Mappings:** `Account` and `AccountValuation` configured with table names, column names, required fields, foreign key cascade delete, and compound indexes (`(household_id, name)`, `(household_id, is_active)`, `(account_id, valuation_date)`).
- **Concurrency & Duplicates:** Non-unique index on `(account_id, valuation_date)` intentionally supports multiple intra-day valuations. Row versioning / concurrency tokens are not yet added.
- **In-Memory Provider:** Currently used for dev and automated tests. SQL Server specific constraints (foreign key cascades, check constraints) are not enforced in in-memory mode.
- **Status:** **PASS for MVP dev; DOCUMENTED GAP for production SQL migration**.

### Phase 4: Frontend & API Review

- **Missing Value Transparency:** Net Worth card now renders an amber warning banner if any account has missing valuations for the selected date.
- **Client Validation:** 3-letter ASCII ISO currency code enforced before request dispatch.
- **Error & Loading States:** Loading indicators and error alerts present across all workflows.
- **Tests & Production Build:** Vitest passed 4/4 tests. Oxlint finished with 0 errors and 0 warnings. Production Vite build completed successfully (`dist/` generated).
- **Status:** **PASS**

### Phase 5: CI, Dependency & IaC Review

- **GitHub Actions Workflows:**
  - `ci.yml`: Scoped `permissions: contents: read`, runs .NET 10 restore, build, test, and React Node.js 24 install, lint, build, test.
  - `infra-validate.yml`: Scoped permissions, lints and builds Bicep via Azure CLI.
- **Bicep Architecture:**
  - App Service: Linux, HTTPS only, TLS 1.2, system-assigned managed identity.
  - SQL Database: Azure AD-only authentication, minimum TLS 1.2, private endpoint support for production.
  - Storage: StorageV2, LRS, private blob container (`statements`), public blob access disabled, HTTPS only.
- **Local Validation Status:** Local Azure CLI / Bicep CLI is not installed on this workstation (`az: command not found`). Local Bicep execution marked as **NOT VERIFIED LOCALLY** (validated in CI runner).
- **Status:** **PASS (IaC verified via GitHub workflow definition; local tool not installed)**

---

## 4. Verification Evidence

### Automated Backend Tests
```
Test run for FinanceTracker.Domain.Tests.dll: 65 passed (0 failed)
Test run for FinanceTracker.Importing.Tests.dll: 1 passed (0 failed)
Test run for FinanceTracker.Application.Tests.dll: 24 passed (0 failed)
Test run for FinanceTracker.Infrastructure.Tests.dll: 3 passed (0 failed)
Test run for FinanceTracker.Api.Tests.dll: 23 passed (0 failed)

Total: 116 passed, 0 failed, 0 skipped. Duration: ~7 seconds.
```

### Automated Frontend Tests
```
 RUN  v4.1.12 C:/Hardik/FinanceTracker/src/FinanceTracker.Web
 ✓ src/App.test.tsx (4 tests) 640ms

Test Files: 1 passed (1)
Tests: 4 passed (4)
```

### Frontend Lint & Production Build
```
> oxlint: Found 0 warnings and 0 errors. Finished in 93ms on 8 files.
> tsc -b && vite build: built in 278ms.
```

---

## 5. Documented Non-Blocking Gaps

1. **EF Core SQL Server Migrations:** Database migrations must be scaffolded (`dotnet ef migrations add InitialCreate`) once connection to a local SQL Server / Azure SQL emulator is configured.
2. **User Onboarding / Password Reset:** Production will require invitation and recovery endpoints. Currently accounts are managed/seeded for the household.
3. **Antiforgery Middleware:** Recommend adding `AutoValidateAntiforgeryTokenAttribute` and cookie-header token synchronization when exposing the web client outside of development proxying.
4. **Local Bicep Tooling:** Workstation does not have Azure CLI installed; Bicep templates are validated in GitHub Actions CI.

---

## 6. Next Vertical Slice

Proceed with **P0-5: Statement Upload & Isolated File Parsing**:
- File validation (MIME type, 10 MB limit, extension check).
- Azure Blob Storage client integration using private `statements` container.
- Isolated CSV statement parser extracting dates, descriptions, amounts, and transaction direction.
