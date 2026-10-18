using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Domain.Entities;

/// <summary>
/// Domain entity representing a staged transaction row during the statement import preview lifecycle.
/// Preserves the raw source row, parsed values, validation diagnostics, duplicate status, and user resolutions.
/// Staged rows never touch the financial ledger until an explicit, authorized commit operation succeeds.
/// </summary>
public class ImportStagingRow
{
    // Parameterless constructor for EF Core
    private ImportStagingRow()
    {
        Description = string.Empty;
        Fingerprint = string.Empty;
    }

    public ImportStagingRow(
        Guid id,
        Guid householdId,
        Guid importBatchId,
        int sourceRowNumber,
        string? rawData,
        DateOnly? transactionDate,
        string? description,
        decimal? amount,
        TransactionType? type,
        string? currencyCode,
        string? fingerprint,
        StagingRowValidationStatus validationStatus,
        string? validationErrors,
        DuplicateStatus duplicateStatus = DuplicateStatus.None,
        string? duplicateReason = null,
        Guid? matchedTransactionId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Row Id cannot be empty.", nameof(id));
        }

        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("Household Id cannot be empty.", nameof(householdId));
        }

        if (importBatchId == Guid.Empty)
        {
            throw new ArgumentException("Import batch Id cannot be empty.", nameof(importBatchId));
        }

        if (sourceRowNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceRowNumber), "Source row number must be positive.");
        }

        Id = id;
        HouseholdId = householdId;
        ImportBatchId = importBatchId;
        SourceRowNumber = sourceRowNumber;
        RawData = rawData;
        TransactionDate = transactionDate;
        Description = description?.Trim() ?? string.Empty;
        Amount = amount;
        Type = type;
        CurrencyCode = currencyCode?.Trim().ToUpperInvariant();
        Fingerprint = fingerprint?.Trim().ToLowerInvariant() ?? string.Empty;
        ValidationStatus = validationStatus;
        ValidationErrors = validationErrors?.Trim();
        DuplicateStatus = duplicateStatus;
        DuplicateReason = duplicateReason?.Trim();
        MatchedTransactionId = matchedTransactionId;

        // By default, rows with errors or duplicates are unselected or require resolution
        IsSelected = validationStatus == StagingRowValidationStatus.Valid && duplicateStatus == DuplicateStatus.None;
        Resolution = RowResolution.None;
    }

    public Guid Id { get; private set; }
    public Guid HouseholdId { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public int SourceRowNumber { get; private set; }
    public string? RawData { get; private set; }

    // Parsed candidate fields
    public DateOnly? TransactionDate { get; private set; }
    public string Description { get; private set; }
    public decimal? Amount { get; private set; }
    public TransactionType? Type { get; private set; }
    public string? CurrencyCode { get; private set; }
    public string Fingerprint { get; private set; }

    // Diagnostics & Validation
    public StagingRowValidationStatus ValidationStatus { get; private set; }
    public string? ValidationErrors { get; private set; }

    // Deduplication
    public DuplicateStatus DuplicateStatus { get; private set; }
    public string? DuplicateReason { get; private set; }
    public Guid? MatchedTransactionId { get; private set; }

    // Selection & User Resolution
    public bool IsSelected { get; private set; }
    public RowResolution Resolution { get; private set; }
    public string? ResolutionNotes { get; private set; }
    public DateOnly? ResolvedTransactionDate { get; private set; }
    public string? ResolvedDescription { get; private set; }
    public decimal? ResolvedAmount { get; private set; }
    public TransactionType? ResolvedType { get; private set; }

    // Ledger Linkage
    public Guid? CommittedTransactionId { get; private set; }

    // Navigation
    public ImportBatch ImportBatch { get; private set; } = null!;

    // Derived Effective Values
    public DateOnly? EffectiveDate => ResolvedTransactionDate ?? TransactionDate;
    public string EffectiveDescription => !string.IsNullOrWhiteSpace(ResolvedDescription) ? ResolvedDescription : Description;
    public decimal? EffectiveAmount => ResolvedAmount ?? Amount;
    public TransactionType? EffectiveType => ResolvedType ?? Type;

    public bool IsEligibleForCommit =>
        IsSelected &&
        Resolution != RowResolution.Excluded &&
        ValidationStatus != StagingRowValidationStatus.Invalid &&
        (DuplicateStatus == DuplicateStatus.None || Resolution == RowResolution.ApprovedAsIs || Resolution == RowResolution.ManuallyCorrected) &&
        EffectiveDate.HasValue &&
        !string.IsNullOrWhiteSpace(EffectiveDescription) &&
        EffectiveAmount.HasValue &&
        EffectiveAmount.Value > 0 &&
        EffectiveType.HasValue;

    public void SetSelection(bool isSelected)
    {
        IsSelected = isSelected;
        if (!isSelected && Resolution == RowResolution.None)
        {
            Resolution = RowResolution.Excluded;
        }
        else if (isSelected && Resolution == RowResolution.Excluded)
        {
            Resolution = RowResolution.None;
        }
    }

    public void ResolveApprovedAsIs(string? notes = null)
    {
        if (ValidationStatus == StagingRowValidationStatus.Invalid)
        {
            throw new InvalidOperationException("Cannot approve an invalid row without correcting its validation errors.");
        }

        Resolution = RowResolution.ApprovedAsIs;
        ResolutionNotes = notes?.Trim();
        IsSelected = true;
    }

    public void ResolveManuallyCorrected(
        DateOnly? newDate,
        string? newDescription,
        decimal? newAmount,
        TransactionType? newType,
        string? notes = null)
    {
        if (newAmount.HasValue && newAmount.Value <= 0)
        {
            throw new ArgumentException("Corrected amount must be greater than zero.", nameof(newAmount));
        }

        if (newDescription != null && string.IsNullOrWhiteSpace(newDescription))
        {
            throw new ArgumentException("Corrected description cannot be whitespace.", nameof(newDescription));
        }

        ResolvedTransactionDate = newDate;
        ResolvedDescription = newDescription?.Trim();
        ResolvedAmount = newAmount;
        ResolvedType = newType;
        Resolution = RowResolution.ManuallyCorrected;
        ResolutionNotes = notes?.Trim();

        // Recheck if now valid
        if (EffectiveDate.HasValue && !string.IsNullOrWhiteSpace(EffectiveDescription) && EffectiveAmount.HasValue && EffectiveAmount.Value > 0 && EffectiveType.HasValue)
        {
            ValidationStatus = StagingRowValidationStatus.Valid;
            ValidationErrors = null;
            IsSelected = true;
        }
    }

    public void ResolveExcluded(string? reason = null)
    {
        Resolution = RowResolution.Excluded;
        ResolutionNotes = reason?.Trim();
        IsSelected = false;
    }

    public void UpdateDuplicateStatus(DuplicateStatus status, string? reason, Guid? matchedTxId, string? fingerprint)
    {
        DuplicateStatus = status;
        DuplicateReason = reason?.Trim();
        MatchedTransactionId = matchedTxId;
        if (!string.IsNullOrWhiteSpace(fingerprint))
        {
            Fingerprint = fingerprint.Trim().ToLowerInvariant();
        }

        if (status != DuplicateStatus.None && Resolution == RowResolution.None)
        {
            // By default, duplicate rows should not be auto-selected for import
            IsSelected = false;
        }
    }

    public void MarkCommitted(Guid transactionId)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Committed transaction Id cannot be empty.", nameof(transactionId));
        }

        CommittedTransactionId = transactionId;
    }
}
