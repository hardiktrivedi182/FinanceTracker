using FinanceTracker.Application.Accounts.Dtos;

namespace FinanceTracker.Application.Accounts.Services;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> GetAccountsAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<AccountDto?> GetAccountByIdAsync(Guid id, CancellationToken ct = default);
    Task<AccountDto> CreateAccountAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<AccountDto> UpdateAccountAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default);
    Task<bool> DeactivateAccountAsync(Guid id, CancellationToken ct = default);
    Task<AccountValuationDto> RecordValuationAsync(Guid accountId, RecordValuationRequest request, CancellationToken ct = default);
    Task<AccountValuationDto> UpdateValuationAsync(Guid accountId, Guid valuationId, UpdateValuationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AccountValuationDto>> GetValuationsAsync(Guid accountId, CancellationToken ct = default);
}
