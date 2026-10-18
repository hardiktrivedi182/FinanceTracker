using FinanceTracker.Application.Accounts.Persistence;
using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.ImportBatches.Dtos;
using FinanceTracker.Application.ImportBatches.Persistence;
using FinanceTracker.Application.Statements.Parsing;
using FinanceTracker.Application.Statements.Persistence;
using FinanceTracker.Application.Statements.Storage;
using FinanceTracker.Application.Transactions.Persistence;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.ImportBatches.Services;

public class ImportBatchService : IImportBatchService
{
    private readonly IImportBatchRepository _batchRepository;
    private readonly IStatementRepository _statementRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IStatementStorage _statementStorage;
    private readonly ICsvStatementParser _csvParser;
    private readonly IImportFingerprintService _fingerprintService;
    private readonly IHouseholdContext _householdContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ImportBatchService(
        IImportBatchRepository batchRepository,
        IStatementRepository statementRepository,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IStatementStorage statementStorage,
        ICsvStatementParser csvParser,
        IImportFingerprintService fingerprintService,
        IHouseholdContext householdContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _batchRepository = batchRepository;
        _statementRepository = statementRepository;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _statementStorage = statementStorage;
        _csvParser = csvParser;
        _fingerprintService = fingerprintService;
        _householdContext = householdContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<ImportBatchDetailDto> CreateBatchAsync(Guid statementId, Guid accountId, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var householdId = _householdContext.HouseholdId;
        var userId = _currentUser.UserId!;

        // 1. Verify Statement
        var statement = await _statementRepository.GetByIdAsync(statementId, ct)
            ?? throw new KeyNotFoundException($"Statement '{statementId}' was not found.");

        if (statement.HouseholdId != householdId)
        {
            throw new UnauthorizedAccessException("Statement does not belong to the active household.");
        }

        if (statement.Status == StatementProcessingStatus.Committed)
        {
            throw new InvalidOperationException("This statement has already been committed to the ledger.");
        }

        // 2. Verify Target Account
        var account = await _accountRepository.GetByIdAsync(householdId, accountId, ct)
            ?? throw new KeyNotFoundException($"Account '{accountId}' was not found.");

        if (account.HouseholdId != householdId)
        {
            throw new UnauthorizedAccessException("Account does not belong to the active household.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException($"Cannot import statement into deactivated account '{account.Name}'.");
        }

        EnsureAccountWriteAccess(account, userId);

        // 3. Open statement stream and parse
        await using var stream = await _statementStorage.OpenReadAsync(statement.StorageKey, ct);
        var parseResult = await _csvParser.ParseAsync(stream, ct);

        // 4. Create Batch aggregate
        var batchId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();
        var batch = new ImportBatch(batchId, householdId, statementId, accountId, userId, now);

        // 5. Load existing account transactions for deduplication
        var existingTransactions = await _transactionRepository.GetByAccountIdAsync(accountId, ct);
        var existingTxFingerprints = existingTransactions
            .GroupBy(t => t.Fingerprint)
            .ToDictionary(g => g.Key, g => g.First());

        // Track batch-internal fingerprints to detect duplicates within this statement
        var seenBatchFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 6. Transform parsed rows into Staging Rows
        foreach (var row in parseResult.Rows)
        {
            var rowType = string.Equals(row.Direction, "Credit", StringComparison.OrdinalIgnoreCase)
                ? TransactionType.Credit
                : TransactionType.Debit;

            var fingerprint = _fingerprintService.ComputeFingerprint(
                accountId,
                row.Date,
                row.Description,
                row.Amount,
                rowType,
                account.CurrencyCode);

            var validationStatus = StagingRowValidationStatus.Valid;
            string? validationError = null;

            if (row.Amount <= 0)
            {
                validationStatus = StagingRowValidationStatus.Invalid;
                validationError = "Transaction amount must be greater than zero.";
            }
            else if (string.IsNullOrWhiteSpace(row.Description))
            {
                validationStatus = StagingRowValidationStatus.Invalid;
                validationError = "Transaction description cannot be empty.";
            }

            var duplicateStatus = DuplicateStatus.None;
            string? duplicateReason = null;
            Guid? matchedTxId = null;

            if (existingTxFingerprints.TryGetValue(fingerprint, out var matchedTx))
            {
                duplicateStatus = DuplicateStatus.PossibleDuplicate;
                duplicateReason = $"Possible duplicate: matches existing transaction on {matchedTx.TransactionDate:yyyy-MM-dd} for {matchedTx.Amount:F2} ({matchedTx.Description}).";
                matchedTxId = matchedTx.Id;
            }
            else if (!seenBatchFingerprints.Add(fingerprint))
            {
                duplicateStatus = DuplicateStatus.PossibleDuplicate;
                duplicateReason = "Possible duplicate: another row with identical date, description, amount, and type appears earlier in this import batch.";
            }

            var stagingRow = new ImportStagingRow(
                Guid.NewGuid(),
                householdId,
                batchId,
                row.RowNumber,
                null,
                row.Date,
                row.Description,
                row.Amount,
                rowType,
                account.CurrencyCode,
                fingerprint,
                validationStatus,
                validationError,
                duplicateStatus,
                duplicateReason,
                matchedTxId);

            batch.AddRow(stagingRow);
        }

        // Also add any parser-level errors with row numbers as invalid staging rows so user can see and resolve/exclude them
        foreach (var err in parseResult.Errors.Where(e => e.RowNumber.HasValue))
        {
            var rowNum = err.RowNumber!.Value;
            // Check if already represented
            if (batch.Rows.Any(r => r.SourceRowNumber == rowNum))
            {
                continue;
            }

            var errRow = new ImportStagingRow(
                Guid.NewGuid(),
                householdId,
                batchId,
                rowNum,
                null,
                null,
                "Unparseable row",
                null,
                null,
                account.CurrencyCode,
                string.Empty,
                StagingRowValidationStatus.Invalid,
                err.Message,
                DuplicateStatus.None,
                null,
                null);

            batch.AddRow(errRow);
        }

        await _batchRepository.AddAsync(batch, ct);

        // Fetch back with eager loaded navigations
        var loaded = await _batchRepository.GetByIdWithRowsAsync(batchId, ct)
            ?? throw new InvalidOperationException("Failed to retrieve created import batch.");

        return MapToDetailDto(loaded);
    }

    public async Task<ImportBatchDetailDto?> GetBatchByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var batch = await _batchRepository.GetByIdWithRowsAsync(id, ct);
        if (batch == null || batch.HouseholdId != _householdContext.HouseholdId)
        {
            return null;
        }

        return MapToDetailDto(batch);
    }

    public async Task<IReadOnlyList<ImportBatchSummaryDto>> GetBatchesAsync(Guid? statementId = null, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var batches = await _batchRepository.GetByHouseholdIdAsync(_householdContext.HouseholdId, statementId, ct);
        return batches.Select(b => MapToSummaryDto(b, b.Statement?.OriginalFileName ?? string.Empty, b.Account?.Name ?? string.Empty, b.Account?.CurrencyCode ?? string.Empty)).ToList();
    }

    public async Task<ImportBatchDetailDto> UpdateStagingRowAsync(Guid batchId, Guid rowId, UpdateStagingRowRequest request, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var batch = await _batchRepository.GetByIdWithRowsAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Import batch '{batchId}' was not found.");

        if (batch.HouseholdId != _householdContext.HouseholdId)
        {
            throw new UnauthorizedAccessException("Import batch does not belong to the active household.");
        }

        if (batch.Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Cannot modify rows in an already committed import batch.");
        }

        if (batch.Status == ImportBatchStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot modify rows in a cancelled import batch.");
        }

        var account = batch.Account ?? await _accountRepository.GetByIdAsync(_householdContext.HouseholdId, batch.AccountId, ct)
            ?? throw new KeyNotFoundException($"Account '{batch.AccountId}' was not found.");
        EnsureAccountWriteAccess(account, _currentUser.UserId!);

        var row = batch.Rows.FirstOrDefault(r => r.Id == rowId)
            ?? throw new KeyNotFoundException($"Staging row '{rowId}' was not found in batch '{batchId}'.");

        if (request.Resolution.HasValue)
        {
            switch (request.Resolution.Value)
            {
                case RowResolution.ApprovedAsIs:
                    row.ResolveApprovedAsIs(request.ResolutionNotes);
                    break;
                case RowResolution.Excluded:
                    row.ResolveExcluded(request.ResolutionNotes);
                    break;
                case RowResolution.ManuallyCorrected:
                    row.ResolveManuallyCorrected(
                        request.ResolvedDate,
                        request.ResolvedDescription,
                        request.ResolvedAmount,
                        request.ResolvedType,
                        request.ResolutionNotes);

                    if (row.EffectiveDate.HasValue && !string.IsNullOrWhiteSpace(row.EffectiveDescription) && row.EffectiveAmount.HasValue && row.EffectiveType.HasValue)
                    {
                        var newFingerprint = _fingerprintService.ComputeFingerprint(
                            batch.AccountId,
                            row.EffectiveDate.Value,
                            row.EffectiveDescription,
                            row.EffectiveAmount.Value,
                            row.EffectiveType.Value,
                            account.CurrencyCode);
                        row.UpdateDuplicateStatus(row.DuplicateStatus, row.DuplicateReason, row.MatchedTransactionId, newFingerprint);
                    }
                    break;
                case RowResolution.None:
                    if (request.IsSelected.HasValue)
                    {
                        row.SetSelection(request.IsSelected.Value);
                    }
                    break;
            }
        }
        else if (request.IsSelected.HasValue)
        {
            row.SetSelection(request.IsSelected.Value);
        }

        batch.RecalculateCountsAndStatus();
        await _batchRepository.UpdateAsync(batch, ct);

        return MapToDetailDto(batch);
    }

    public async Task<ImportBatchDetailDto> BulkUpdateRowsAsync(Guid batchId, BulkUpdateRowsRequest request, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var batch = await _batchRepository.GetByIdWithRowsAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Import batch '{batchId}' was not found.");

        if (batch.HouseholdId != _householdContext.HouseholdId)
        {
            throw new UnauthorizedAccessException("Import batch does not belong to the active household.");
        }

        if (batch.Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Cannot modify rows in an already committed import batch.");
        }

        if (batch.Status == ImportBatchStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot modify rows in a cancelled import batch.");
        }

        var account = batch.Account ?? await _accountRepository.GetByIdAsync(_householdContext.HouseholdId, batch.AccountId, ct)
            ?? throw new KeyNotFoundException($"Account '{batch.AccountId}' was not found.");
        EnsureAccountWriteAccess(account, _currentUser.UserId!);

        var targetRows = request.SelectAll == true
            ? batch.Rows
            : batch.Rows.Where(r => request.RowIds?.Contains(r.Id) == true).ToList();

        foreach (var row in targetRows)
        {
            if (request.Resolution.HasValue)
            {
                if (request.Resolution.Value == RowResolution.ApprovedAsIs && row.ValidationStatus != StagingRowValidationStatus.Invalid)
                {
                    row.ResolveApprovedAsIs(request.ResolutionNotes);
                }
                else if (request.Resolution.Value == RowResolution.Excluded)
                {
                    row.ResolveExcluded(request.ResolutionNotes);
                }
            }
            else if (request.IsSelected.HasValue)
            {
                if (request.IsSelected.Value && row.ValidationStatus == StagingRowValidationStatus.Invalid)
                {
                    continue;
                }
                row.SetSelection(request.IsSelected.Value);
            }
        }

        batch.RecalculateCountsAndStatus();
        await _batchRepository.UpdateAsync(batch, ct);

        return MapToDetailDto(batch);
    }

    public async Task<CommitImportBatchResultDto> CommitBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        EnsureHouseholdAccess();

        var householdId = _householdContext.HouseholdId;
        var userId = _currentUser.UserId!;
        var now = _timeProvider.GetUtcNow();

        var batch = await _batchRepository.GetByIdWithRowsAsync(batchId, ct)
            ?? throw new KeyNotFoundException($"Import batch '{batchId}' was not found.");

        if (batch.HouseholdId != householdId)
        {
            throw new UnauthorizedAccessException("Import batch does not belong to the active household.");
        }

        // Idempotency check: if already committed, return the existing result safely without creating duplicates
        if (batch.Status == ImportBatchStatus.Committed)
        {
            return new CommitImportBatchResultDto(
                batch.Id,
                batch.StatementId,
                batch.AccountId,
                batch.Status,
                batch.CommittedCount ?? 0,
                batch.Rows.Count(r => !r.IsSelected || r.Resolution == RowResolution.Excluded),
                0,
                batch.CommittedAt ?? now,
                $"Import batch was already committed on {batch.CommittedAt:yyyy-MM-dd HH:mm:ss} UTC. Idempotent commit succeeded without duplicating transactions.");
        }

        if (batch.Status == ImportBatchStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot commit a cancelled import batch.");
        }

        if (batch.Statement != null && batch.Statement.Status == StatementProcessingStatus.Failed)
        {
            throw new InvalidOperationException("Cannot commit an import batch for a failed statement.");
        }

        // Revalidate selected rows on the server
        var selectedRows = batch.Rows.Where(r => r.IsSelected).ToList();
        if (selectedRows.Count == 0)
        {
            throw new InvalidOperationException("Cannot commit import batch: no candidate rows are selected for import.");
        }

        // Verify account is active and belongs to household
        var account = await _accountRepository.GetByIdAsync(householdId, batch.AccountId, ct)
            ?? throw new KeyNotFoundException($"Target account '{batch.AccountId}' was not found.");

        if (account.HouseholdId != householdId || !account.IsActive)
        {
            throw new InvalidOperationException("Target account is inactive or does not belong to the authorized household.");
        }

        EnsureAccountWriteAccess(account, userId);

        var transactionsToPersist = new List<Transaction>();

        foreach (var row in selectedRows)
        {
            if (row.Resolution == RowResolution.Excluded)
            {
                throw new InvalidOperationException($"Cannot commit: row {row.SourceRowNumber} is marked as excluded.");
            }

            if (row.ValidationStatus == StagingRowValidationStatus.Invalid)
            {
                throw new InvalidOperationException(
                    $"Cannot commit: row {row.SourceRowNumber} has unresolved validation errors: '{row.ValidationErrors}'. Correct the row or exclude it before committing.");
            }

            if (row.DuplicateStatus != DuplicateStatus.None &&
                row.Resolution != RowResolution.ApprovedAsIs &&
                row.Resolution != RowResolution.ManuallyCorrected)
            {
                throw new InvalidOperationException(
                    $"Cannot commit: row {row.SourceRowNumber} is flagged as a duplicate and has not been explicitly reviewed and approved.");
            }

            if (!row.EffectiveDate.HasValue ||
                string.IsNullOrWhiteSpace(row.EffectiveDescription) ||
                !row.EffectiveAmount.HasValue ||
                row.EffectiveAmount.Value <= 0 ||
                !row.EffectiveType.HasValue)
            {
                throw new InvalidOperationException(
                    $"Cannot commit: row {row.SourceRowNumber} has invalid or incomplete transaction fields.");
            }

            var transactionId = Guid.NewGuid();
            var tx = new Transaction(
                transactionId,
                householdId,
                batch.AccountId,
                batch.StatementId,
                batch.Id,
                row.SourceRowNumber,
                row.EffectiveDate.Value,
                row.TransactionDate,
                row.EffectiveDescription,
                row.EffectiveAmount.Value,
                row.EffectiveType.Value,
                account.CurrencyCode,
                row.Fingerprint,
                userId,
                now);

            row.MarkCommitted(transactionId);
            transactionsToPersist.Add(tx);
        }

        // Mark batch committed
        batch.MarkCommitted(userId, now, transactionsToPersist.Count);

        // Mark statement committed if present
        if (batch.Statement != null)
        {
            batch.Statement.MarkCommitted();
        }

        // Commit atomically to database with optimistic concurrency conflict detection
        await _batchRepository.CommitBatchAtomicAsync(batch, transactionsToPersist, batch.Statement, ct);

        var excludedCount = batch.Rows.Count(r => !r.IsSelected || r.Resolution == RowResolution.Excluded);

        return new CommitImportBatchResultDto(
            batch.Id,
            batch.StatementId,
            batch.AccountId,
            batch.Status,
            transactionsToPersist.Count,
            excludedCount,
            0,
            now,
            $"Successfully committed {transactionsToPersist.Count} transactions into '{account.Name}'.");
    }

    private void EnsureAccountWriteAccess(Account account, string currentUserId)
    {
        if (account.OwnershipType == OwnershipType.Individual)
        {
            bool isOwner = string.Equals(account.OwnerUserId, currentUserId, StringComparison.Ordinal);
            bool isAdmin = _householdContext.Role == HouseholdRole.Admin;

            if (!isOwner && !isAdmin)
            {
                throw new UnauthorizedAccessException(
                    "Only the account owner or a household admin may modify this individual account or its import batches.");
            }
        }
    }

    private void EnsureHouseholdAccess()
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            throw new UnauthorizedAccessException("Authentication is required for import operations.");
        }

        if (!_householdContext.HasHousehold)
        {
            throw new InvalidOperationException("Current user is not associated with an active household.");
        }
    }

    private static ImportBatchDetailDto MapToDetailDto(ImportBatch batch)
    {
        var summary = MapToSummaryDto(
            batch,
            batch.Statement?.OriginalFileName ?? string.Empty,
            batch.Account?.Name ?? string.Empty,
            batch.Account?.CurrencyCode ?? string.Empty);

        var rows = batch.Rows
            .OrderBy(r => r.SourceRowNumber)
            .Select(r => new ImportStagingRowDto(
                r.Id,
                r.SourceRowNumber,
                r.RawData,
                r.TransactionDate,
                r.Description,
                r.Amount,
                r.Type,
                r.CurrencyCode,
                r.Fingerprint,
                r.ValidationStatus,
                r.ValidationErrors,
                r.DuplicateStatus,
                r.DuplicateReason,
                r.MatchedTransactionId,
                r.IsSelected,
                r.Resolution,
                r.ResolutionNotes,
                r.ResolvedTransactionDate,
                r.ResolvedDescription,
                r.ResolvedAmount,
                r.ResolvedType,
                r.EffectiveDate,
                r.EffectiveDescription,
                r.EffectiveAmount,
                r.EffectiveType,
                r.IsEligibleForCommit,
                r.CommittedTransactionId))
            .ToList();

        return new ImportBatchDetailDto(summary, rows);
    }

    private static ImportBatchSummaryDto MapToSummaryDto(
        ImportBatch b,
        string fileName,
        string accountName,
        string currency)
    {
        return new ImportBatchSummaryDto(
            b.Id,
            b.HouseholdId,
            b.StatementId,
            fileName,
            b.AccountId,
            accountName,
            currency,
            b.UploadedByUserId,
            b.Status,
            b.TotalRowCount,
            b.ReadyCount,
            b.DuplicateCount,
            b.ErrorCount,
            b.SelectedRowCount,
            b.CommittedCount,
            b.CreatedAt,
            b.CommittedAt,
            b.CommittedByUserId);
    }
}
