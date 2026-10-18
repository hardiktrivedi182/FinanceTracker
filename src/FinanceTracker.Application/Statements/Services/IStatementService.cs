using FinanceTracker.Application.Statements.Dtos;

namespace FinanceTracker.Application.Statements.Services;

public interface IStatementService
{
    Task<StatementUploadResultDto> UploadAndParseAsync(
        string originalFileName,
        long fileSizeBytes,
        Stream fileContentStream,
        CancellationToken ct = default);

    Task<IReadOnlyList<StatementSummaryDto>> GetStatementsAsync(CancellationToken ct = default);

    Task<StatementSummaryDto?> GetStatementByIdAsync(Guid id, CancellationToken ct = default);
}
