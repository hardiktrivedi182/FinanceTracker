using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.ImportBatches.Dtos;

public record ImportBatchSummaryDto(
    Guid Id,
    Guid HouseholdId,
    Guid StatementId,
    string OriginalFileName,
    Guid AccountId,
    string AccountName,
    string AccountCurrency,
    string UploadedByUserId,
    ImportBatchStatus Status,
    int TotalRowCount,
    int ReadyCount,
    int DuplicateCount,
    int ErrorCount,
    int SelectedRowCount,
    int? CommittedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt,
    string? CommittedByUserId);

public record ImportStagingRowDto(
    Guid Id,
    int SourceRowNumber,
    string? RawData,
    DateOnly? TransactionDate,
    string Description,
    decimal? Amount,
    TransactionType? Type,
    string? CurrencyCode,
    string Fingerprint,
    StagingRowValidationStatus ValidationStatus,
    string? ValidationErrors,
    DuplicateStatus DuplicateStatus,
    string? DuplicateReason,
    Guid? MatchedTransactionId,
    bool IsSelected,
    RowResolution Resolution,
    string? ResolutionNotes,
    DateOnly? ResolvedTransactionDate,
    string? ResolvedDescription,
    decimal? ResolvedAmount,
    TransactionType? ResolvedType,
    DateOnly? EffectiveDate,
    string EffectiveDescription,
    decimal? EffectiveAmount,
    TransactionType? EffectiveType,
    bool IsEligibleForCommit,
    Guid? CommittedTransactionId);

public record ImportBatchDetailDto(
    ImportBatchSummaryDto Batch,
    IReadOnlyList<ImportStagingRowDto> Rows);

public record CreateImportBatchRequest(
    Guid StatementId,
    Guid AccountId);

public record UpdateStagingRowRequest(
    bool? IsSelected,
    RowResolution? Resolution,
    string? ResolutionNotes,
    DateOnly? ResolvedDate,
    string? ResolvedDescription,
    decimal? ResolvedAmount,
    TransactionType? ResolvedType);

public record BulkUpdateRowsRequest(
    IReadOnlyList<Guid>? RowIds,
    bool? SelectAll,
    bool? IsSelected,
    RowResolution? Resolution,
    string? ResolutionNotes);

public record CommitImportBatchResultDto(
    Guid BatchId,
    Guid StatementId,
    Guid AccountId,
    ImportBatchStatus Status,
    int ImportedCount,
    int ExcludedCount,
    int UnresolvedCount,
    DateTimeOffset CommittedAt,
    string Message);
