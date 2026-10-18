using FinanceTracker.Domain.Enums;

namespace FinanceTracker.Application.Statements.Dtos;

public record StatementSummaryDto(
    Guid Id,
    Guid HouseholdId,
    string UploaderUserId,
    string OriginalFileName,
    StatementFormat Format,
    long FileSizeBytes,
    DateTimeOffset UploadedAt,
    StatementProcessingStatus Status,
    int? ParsedTransactionCount,
    string? ErrorMessage);

public record ParsedTransactionRowDto(
    int RowNumber,
    DateOnly Date,
    string Description,
    decimal Amount,
    string Direction,
    decimal? Balance);

public record StatementParseErrorDto(
    int? RowNumber,
    string Message);

public record StatementUploadResultDto(
    Guid StatementId,
    string OriginalFileName,
    StatementProcessingStatus Status,
    int? ParsedTransactionCount,
    string? ErrorMessage,
    IReadOnlyList<ParsedTransactionRowDto>? SampleRows,
    IReadOnlyList<StatementParseErrorDto>? Errors,
    string Message = "Statement uploaded and parsed. No transactions were imported into the ledger.",
    Guid? ImportBatchId = null);
