using FinanceTracker.Application.Accounts.Dtos;
using FinanceTracker.Application.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AccountDto>>> GetAccounts(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var accounts = await _accountService.GetAccountsAsync(includeInactive, ct);
        return Ok(accounts);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountDto>> GetAccountById(Guid id, CancellationToken ct = default)
    {
        var account = await _accountService.GetAccountByIdAsync(id, ct);
        if (account == null)
        {
            return NotFound();
        }

        return Ok(account);
    }

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountDto>> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var created = await _accountService.CreateAccountAsync(request, ct);
            return CreatedAtAction(nameof(GetAccountById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> UpdateAccount(
        Guid id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var updated = await _accountService.UpdateAccountAsync(id, request, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateAccount(Guid id, CancellationToken ct = default)
    {
        try
        {
            var success = await _accountService.DeactivateAccountAsync(id, ct);
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/valuations")]
    [ProducesResponseType(typeof(AccountValuationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountValuationDto>> RecordValuation(
        Guid id,
        [FromBody] RecordValuationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var valuation = await _accountService.RecordValuationAsync(id, request, ct);
            return StatusCode(StatusCodes.Status201Created, valuation);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/valuations/{valuationId:guid}")]
    [ProducesResponseType(typeof(AccountValuationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountValuationDto>> UpdateValuation(
        Guid id,
        Guid valuationId,
        [FromBody] UpdateValuationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var valuation = await _accountService.UpdateValuationAsync(id, valuationId, request, ct);
            return Ok(valuation);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/valuations")]
    [ProducesResponseType(typeof(IReadOnlyList<AccountValuationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<AccountValuationDto>>> GetValuations(Guid id, CancellationToken ct = default)
    {
        try
        {
            var valuations = await _accountService.GetValuationsAsync(id, ct);
            return Ok(valuations);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
