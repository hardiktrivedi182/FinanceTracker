using FinanceTracker.Application.ImportBatches.Dtos;

namespace FinanceTracker.Application.ImportBatches.Services;

public interface IImportBatchService
{
    Task<ImportBatchDetailDto> CreateBatchAsync(Guid statementId, Guid accountId, CancellationToken ct = default);
    Task<ImportBatchDetailDto?> GetBatchByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ImportBatchSummaryDto>> GetBatchesAsync(Guid? statementId = null, CancellationToken ct = default);
    Task<ImportBatchDetailDto> UpdateStagingRowAsync(Guid batchId, Guid rowId, UpdateStagingRowRequest request, CancellationToken ct = default);
    Task<ImportBatchDetailDto> BulkUpdateRowsAsync(Guid batchId, BulkUpdateRowsRequest request, CancellationToken ct = default);
    Task<CommitImportBatchResultDto> CommitBatchAsync(Guid batchId, CancellationToken ct = default);
}
