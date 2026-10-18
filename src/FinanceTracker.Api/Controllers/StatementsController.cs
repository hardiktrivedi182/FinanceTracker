using FinanceTracker.Application.Statements.Dtos;
using FinanceTracker.Application.Statements.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/statements")]
[Authorize]
public class StatementsController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB limit
    private readonly IStatementService _statementService;

    public StatementsController(IStatementService statementService)
    {
        _statementService = statementService;
    }

    /// <summary>
    /// Uploads a CSV statement file, securely stores it in isolated private storage,
    /// parses transaction candidates, and returns the processing result.
    /// Does NOT import or mutate financial ledger transactions.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(StatementUploadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<StatementUploadResultDto>> UploadStatement(
        IFormFile? file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "A non-empty CSV file must be provided." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { error = "File size exceeds the 10 MB limit." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _statementService.UploadAndParseAsync(
                file.FileName,
                file.Length,
                stream,
                ct);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Lists all statements uploaded by members of the authenticated household.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StatementSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<StatementSummaryDto>>> GetStatements(CancellationToken ct = default)
    {
        var statements = await _statementService.GetStatementsAsync(ct);
        return Ok(statements);
    }

    /// <summary>
    /// Returns summary metadata and processing status for a single statement within the household.
    /// Never returns raw storage keys, private blob URLs, or storage credentials.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StatementSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<StatementSummaryDto>> GetStatementById(Guid id, CancellationToken ct = default)
    {
        var statement = await _statementService.GetStatementByIdAsync(id, ct);
        if (statement == null)
        {
            return NotFound();
        }

        return Ok(statement);
    }
}
