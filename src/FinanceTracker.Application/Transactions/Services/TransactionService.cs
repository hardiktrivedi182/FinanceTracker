using FinanceTracker.Application.Common.Interfaces;
using FinanceTracker.Application.Transactions.Dtos;
using FinanceTracker.Application.Transactions.Persistence;

namespace FinanceTracker.Application.Transactions.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IHouseholdContext _householdContext;
    private readonly ICurrentUser _currentUser;

    public TransactionService(
        ITransactionRepository transactionRepository,
        IHouseholdContext householdContext,
        ICurrentUser currentUser)
    {
        _transactionRepository = transactionRepository;
        _householdContext = householdContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TransactionDto>> GetTransactionsAsync(
        Guid? accountId = null,
        Guid? importBatchId = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
        {
            throw new UnauthorizedAccessException("Authentication is required to view transactions.");
        }

        if (!_householdContext.HasHousehold)
        {
            throw new InvalidOperationException("Current user is not associated with an active household.");
        }

        var transactions = await _transactionRepository.GetByHouseholdIdAsync(
            _householdContext.HouseholdId,
            accountId,
            importBatchId,
            startDate,
            endDate,
            ct);

        return transactions.Select(t => new TransactionDto(
            t.Id,
            t.HouseholdId,
            t.AccountId,
            t.StatementId,
            t.ImportBatchId,
            t.SourceRowNumber,
            t.TransactionDate,
            t.OriginalTransactionDate,
            t.Description,
            t.Amount,
            t.Type,
            t.SignedAmount,
            t.CurrencyCode,
            t.Fingerprint,
            t.CreatedAt)).ToList();
    }
}
