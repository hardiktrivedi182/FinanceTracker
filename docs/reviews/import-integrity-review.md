# Safebox P0-6: Import Integrity and Financial Ledger Release Gate Review

**Review Date:** October 18, 2026  
**Scope:** Persisted Import Batches, Staging Rows Review, Duplicate Fingerprint Detection, Explicit Ledger Commit, Concurrency & Idempotency, Tenancy & Authorization, Relational Constraints, and Regression Testing.  
**Reviewer:** Antigravity Autonomous Security & Correctness Auditor  

---

## Executive Summary & Recommendation

### Outcome: **Ready for transaction categorization and reconciliation** (with documented non-blocking gaps)

The release gate audit for **P0-6: Statement Import Preview, Ambiguity Detection & Dedup Commit** has been completed against the full solution codebase. The core promises of the ledger release gate—persisted staging batches, duplicate fingerprint detection, explicit confirmation prior to ledger mutation, zero-data-loss rollback, atomic commit, and tenant isolation—have been verified.

During the audit, five critical and high-severity defects were uncovered in account write permissions, batch state transition invariants, zero-amount domain constraints, staging row selection integrity, and optimistic concurrency conflict handling. All five defects have been remediated in code, backed by targeted regression tests, and validated with:
- **195 passing backend tests** (Domain, Application, Infrastructure, Importing, Api), including 5 dedicated SQL Server LocalDB relational integration tests.
- **13 passing frontend tests** in Vitest.
- **Clean frontend linting** (0 errors, 0 warnings) and production build.
- **EF Core migration model validation** showing zero pending schema drift.

All changes remain uncommitted in the local working tree. No Azure deployments were initiated. Development may proceed to **P0-7: Categories & Transaction Management**.

---

## 1. Initial State Claims vs Actual Evidence

| Claimed State | Actual Repository Evidence | Verification Status |
| :--- | :--- | :--- |
| Persisted import batches & staging rows | `ImportBatch` and `ImportStagingRow` entities mapped in EF Core with `import_batches` and `import_staging_rows` tables. | **VERIFIED** |
| Row-level review & diagnostics | Staged rows record `Diagnostics`, `SourceRowNumber`, `ParsedDate`, `ParsedAmount`, and `Resolution`. | **VERIFIED** |
| Canonical duplicate detection | `Sha256TransactionFingerprintCalculator` normalizes payee, amount, currency, direction, and date into a 64-char hex fingerprint. | **VERIFIED** |
| Explicit commit & ledger immutability | Transactions are inserted into `transactions` ledger strictly on `POST /api/import-batches/{id}/commit`. Unapproved duplicates or excluded rows are omitted. | **VERIFIED** |
| 177 backend tests passing | Ran baseline `dotnet test`: 78 Domain, 48 Application, 6 Infrastructure, 11 Importing, 34 Api = 177 passing. | **VERIFIED** |
| 13 frontend tests passing | Ran baseline `npm test`: 13 passing across `App.test.tsx`, `ImportBatchReview.test.tsx`, and `StatementUploader.test.tsx`. | **VERIFIED** |

---

## 2. Findings and Remediation Table

| ID | Severity | Component / File | Description & Vulnerability | Remediation & Regression Test | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **SEC-P06-01** | **High** | `ImportBatchService.cs` | **Individual Account Write Permissions Bypassed during Import:** Non-owner household members could create import batches, edit staged rows, bulk-update selections, and commit transactions to another member's `Individual` account because ownership checks were omitted. | Added `EnsureAccountWriteAccess(Account, string)` check in `CreateBatchAsync`, `CommitBatchAsync`, `UpdateStagingRowAsync`, and `BulkUpdateRowsAsync`. Enforces that individual accounts can only be modified by the owner or household admin. Added 4 regression tests in `ImportBatchServiceTests.cs` and `ImportBatchesEndpointTests.cs` (returns 403 Forbidden). | **FIXED** |
| **FIN-P06-01** | **High** | `ImportBatchService.cs` | **Batch State Transition Integrity (Cancelled Batch Commits):** Batches in `ImportBatchStatus.Cancelled` status could still have their rows edited or be committed to the ledger. Additionally, multiple batches could be created for an already `Committed` statement. | Added invariant guards rejecting commit and row updates on `Cancelled` batches (`InvalidOperationException`). Added check preventing batch creation for statements already in `Committed` status. Added 4 regression tests in `ImportBatchServiceTests.cs`. | **FIXED** |
| **FIN-P06-02** | **Medium** | `Transaction.cs` | **Domain Invariant Allowed Zero-Amount Transactions:** `Transaction` constructor had `if (amount < 0)`, permitting `0.00m` amounts. Financial ledger rules require strictly positive non-zero amounts. | Updated validation to `if (amount <= 0)` in `Transaction.cs`. Added regression test `Constructor_ZeroAmount_ThrowsArgumentException` in `TransactionTests.cs`. | **FIXED** |
| **DATA-P06-01** | **High** | `ImportStagingRow.cs` & `ImportBatchService.cs` | **Invalid Staging Rows Could Be Selected for Commit:** Calling `row.SetSelection(true)` allowed marking unparseable or invalid rows (`ValidationStatus.Invalid`) as selected. In `BulkUpdateRowsAsync`, "Select All" selected invalid rows without resolution. | Enforced invariant in `ImportStagingRow.SetSelection`: throws `InvalidOperationException` if row is `Invalid`. Updated `BulkUpdateRowsAsync` to skip invalid rows during bulk selection. Added regression tests in `ImportStagingRowTests.cs`, `ImportBatchServiceTests.cs`, and `ImportBatchesEndpointTests.cs` (returns 400 Bad Request). | **FIXED** |
| **CONC-P06-01** | **High** | `ImportBatchRepository.cs` & `ImportBatchesController.cs` | **Concurrency Conflict & Idempotency on Commit:** When two commit requests raced or an edit raced with commit, EF Core threw an unhandled `DbUpdateConcurrencyException`, resulting in a 500 error. Retried commits could duplicate ledger rows if concurrency tokens failed to recover state. | Handled `DbUpdateConcurrencyException` in `CommitBatchAtomicAsync`: re-queries database state, checks if batch was committed concurrently, and returns idempotent committed transaction count if already committed. Throws `InvalidOperationException` if an edit raced with commit, which `ImportBatchesController` maps to `409 Conflict`. Added LocalDB integration tests in `ImportBatchRelationalTests.cs`. | **FIXED** |

---

## 3. Detailed Audit by Section

### Section 1: Commit Atomicity and Concurrency

- **Atomic Boundary:** Commit logic in `ImportBatchRepository.CommitBatchAtomicAsync` executes inside an explicit `IDbContextTransaction` (`BeginTransactionAsync`). It inserts selected and eligible ledger `Transaction` records, marks the `ImportBatch` as `Committed`, updates `CommittedAt` and `CommittedByUserId`, and persists changes via `SaveChangesAsync`.
- **Zero-Data-Loss Rollback:** If any entity validation fails or a database exception occurs, the transaction is rolled back via `await transaction.RollbackAsync()` in the catch block. Verified via `CommitBatchAtomicAsync_WhenFailureOccurs_RollsBackTransactionsAndBatchStatus` in `ImportBatchRelationalTests.cs`.
- **Optimistic Concurrency Mapping:**
  - `ImportBatch.RowVersion` is declared as `byte[] RowVersion { get; private set; }`.
  - Mapped in `ImportBatchConfiguration.cs` using `builder.Property(b => b.RowVersion).IsRowVersion().IsRequired()`.
  - In SQL Server, this maps natively to a database-managed `rowversion` / `timestamp` column.
- **Concurrent Commit Idempotency:**
  - Verified with real SQL Server LocalDB integration tests. When two concurrent commits race with the same initial `RowVersion`, one commits successfully and updates `rowversion`. The second catches `DbUpdateConcurrencyException`, re-evaluates the batch state, sees `Status == ImportBatchStatus.Committed`, and returns the existing committed transaction count without re-inserting transactions or failing.
- **Edits Racing with Commit:**
  - If a user edits a staging row while a commit is in flight, `RowVersion` mismatch aborts the edit with HTTP 409 Conflict, preventing partial or inconsistent commits.
- **Provider Support:**
  - Verified against both EF Core InMemory provider and real Microsoft SQL Server (LocalDB).

### Section 2: Duplicate Protection & Fingerprint Analysis

- **Fingerprint Algorithm:**
  - `Sha256TransactionFingerprintCalculator` computes a SHA-256 hash over canonical normalized fields:
    `AccountId | Date(yyyy-MM-dd) | NormalizedPayee | Amount(0.0000) | Direction(Debit/Credit) | Currency(ISO 4217)`
  - Payee normalization trims whitespace, converts to lowercase, and collapses internal whitespace.
- **Duplicate Detection Scopes:**
  1. *Ledger Check:* Query matches candidate fingerprints against existing committed `Transaction` records for the specific `AccountId` and `HouseholdId`.
  2. *Intra-Batch Check:* Staging candidates within the same file are checked against preceding rows in the batch, flagging repeated records within a single statement.
- **Uniqueness Analysis of `(account_id, fingerprint)`:**
  - The database index `ix_transactions_account_fingerprint` is **NON-UNIQUE**.
  - *Architectural Rationale:* A fingerprint match is a duplicate *warning*, not definitive proof that two transactions are identical. In real household finances, legitimate identical transactions frequently occur (e.g., two \$4.50 coffees on the same day, two \$1.00 parking meter fees, or identical subscription charges).
  - Imposing a database `UNIQUE` constraint on `(account_id, fingerprint)` would crash or reject legitimate, user-approved duplicate transactions.
  - The system safely models duplicates as a staging flag (`IsPotentialDuplicate = true`). Users may review diagnostics and explicitly approve legitimate repeated transactions (`row.ApproveDuplicate()`), which permits them to be selected and committed. Idempotency is guaranteed via the atomic commit boundary and `rowversion` concurrency token, not an artificial unique constraint.
- **Unresolved & Excluded Rows:**
  - Excluded rows (`IsSelected == false`) and unapproved duplicates (`DuplicateResolution == Unresolved`) are never committed.
  - Domain invariants prevent selecting `Invalid` rows.

### Section 3: Transaction and Account Correctness

- **Tenancy Enforcement:**
  - Batch creation, staging row queries, and commits verify `account.HouseholdId == householdContext.HouseholdId`. Cross-household access returns 404 or 403.
- **Individual vs Joint Permissions:**
  - Joint accounts can be modified by any authenticated household member.
  - Individual accounts enforce ownership checks: only the owner (`account.OwnerUserId == currentUserId`) or a household admin can upload statements, edit staged rows, or commit transactions to that account.
- **Authoritative Currency & Decimal Precision:**
  - Transaction currency is derived strictly from the target account's ISO 4217 currency.
  - `Amount` is strictly positive (`decimal(19,4)`).
  - `TransactionType` determines credit/debit sign conventions (`SignedAmount = Type == Credit ? Amount : -Amount`).
- **Traceability & Immutability:**
  - Created ledger transactions retain `StatementId`, `ImportBatchId`, `SourceRowNumber`, and `Fingerprint`.
  - Imported transactions do not alter `AccountValuation` or account balance entities, preserving audit integrity.

### Section 4: Staging Workflow and Authorization

- **Client Forgery Prevention:**
  - The client cannot forge `HouseholdId`, `UserId`, `Status`, `EligibilityFlags`, or storage paths.
  - The commit endpoint accepts only `ImportBatchId`. All eligibility decisions, selections, and duplicate resolutions are evaluated from server-persisted staging rows.
- **Committed and Cancelled Batch Protection:**
  - Batches in `Committed` status reject any row update, bulk update, or re-commit.
  - Batches in `Cancelled` status reject row edits and commits with an invalid state transition error.
- **Antiforgery Protection:**
  - All mutating endpoints (`POST`, `PUT`) are protected by `CookieAntiforgeryMiddleware` verifying `X-XSRF-Token` headers.

### Section 5: Migrations and Relational Behavior

- **EF Core Migrations:**
  - Verified migration `20261018025219_AddStatementImportBatchesAndTransactions`.
  - Foreign key constraints:
    - `import_batches -> accounts (CASCADE)`
    - `import_batches -> statements (RESTRICT)`
    - `import_staging_rows -> import_batches (CASCADE)`
    - `transactions -> accounts (CASCADE)`
    - `transactions -> statements (RESTRICT)`
    - `transactions -> import_batches (RESTRICT)`
  - Index coverage:
    - `ix_transactions_account_fingerprint (account_id, fingerprint)` — Non-unique.
    - `ix_transactions_account_date (account_id, transaction_date)`
    - `ix_import_staging_rows_batch_fingerprint (import_batch_id, fingerprint)`
- **SQL Server Integration Testing:**
  - 5 tests implemented in `tests/FinanceTracker.Infrastructure.Tests/Persistence/ImportBatchRelationalTests.cs` using Microsoft SQL Server LocalDB (`mssqllocaldb`).
  - Validated commit atomicity, rollback on failure, concurrent commit idempotency, edit-vs-commit conflict handling, and non-unique fingerprint index behavior.

---

## 4. Test Verification Evidence

### Backend Test Results (`dotnet test`)
```text
Passed!  - Failed: 0, Passed: 80, Skipped: 0, Total: 80 - FinanceTracker.Domain.Tests.dll
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11 - FinanceTracker.Importing.Tests.dll
Passed!  - Failed: 0, Passed: 57, Skipped: 0, Total: 57 - FinanceTracker.Application.Tests.dll
Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11 - FinanceTracker.Infrastructure.Tests.dll
Passed!  - Failed: 0, Passed: 36, Skipped: 0, Total: 36 - FinanceTracker.Api.Tests.dll

Total Backend: 195 passed, 0 failed.
```

### Frontend Test Results (`npm test -- --run`)
```text
✓ src/App.test.tsx (4 tests)
✓ src/components/ImportBatchReview.test.tsx (3 tests)
✓ src/components/StatementUploader.test.tsx (6 tests)

Test Files  3 passed (3)
Tests       13 passed (13)
```

### Frontend Linting & Build
- `npm run lint`: Found 0 warnings and 0 errors across 18 files.
- `npm run build`: Production bundle generated cleanly (`dist/` built in 602ms).

### EF Core Migration Validation
- `dotnet ef migrations has-pending-model-changes`:
```text
Build succeeded.
No changes have been made to the model since the last migration.
```

---

## 5. Checks That Could Not Be Run Locally & Limitations

1. **Azure CLI / Bicep Local Linting:**
   - Azure CLI (`az`) is not installed on this local workstation (`command not found`).
   - Bicep templates are validated in GitHub Actions CI (`infra-validate.yml`). No live Azure deployments were attempted, adhering to strict safety constraints.
2. **OFX / QFX / PDF File Parsing:**
   - Current parser implementation supports generic RFC 4180 CSV formats. OFX, QFX, and PDF statement parsing are scheduled for subsequent iterations.
3. **Database Provider Parity:**
   - Local development uses EF Core InMemory provider by default; relational constraints and concurrency tokens were verified via isolated SQL Server LocalDB integration tests. Production deployment requires running EF migrations against Azure SQL Database.

---

## 6. Final Recommendation

### **Ready for transaction categorization and reconciliation** (with documented non-blocking gaps)

The import integrity and financial ledger release gate is **PASSED**. The ledger mutation pathway is atomic, tenant-isolated, concurrent-safe, and idempotent. All P0-6 acceptance criteria have been verified with 195 backend tests and 13 frontend tests. Development may safely proceed to **P0-7: Categories & Transaction Management**.
