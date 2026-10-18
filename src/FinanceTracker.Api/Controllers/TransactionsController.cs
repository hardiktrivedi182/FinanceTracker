using FinanceTracker.Application.Transactions.Dtos;
using FinanceTracker.Application.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/transactions")]
[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>
    /// Lists committed transactions for the authenticated household, with optional filters for account, batch, and date range.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetTransactions(
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? importBatchId,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        CancellationToken ct = default)
    {
        var transactions = await _transactionService.GetTransactionsAsync(
            accountId,
            importBatchId,
            startDate,
            endDate,
            ct);

        return Ok(transactions);
    }
}
