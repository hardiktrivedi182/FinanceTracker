using FinanceTracker.Application.ImportBatches.Dtos;
using FinanceTracker.Application.ImportBatches.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/import-batches")]
[Authorize]
public class ImportBatchesController : ControllerBase
{
    private readonly IImportBatchService _batchService;

    public ImportBatchesController(IImportBatchService batchService)
    {
        _batchService = batchService;
    }

    /// <summary>
    /// Creates a reviewable import batch for an uploaded statement and target account.
    /// Runs parsing, initial row-level validation, and duplicate detection.
    /// Does NOT import transactions into the ledger.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ImportBatchDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchDetailDto>> CreateBatch(
        [FromBody] CreateImportBatchRequest request,
        CancellationToken ct = default)
    {
        if (request.StatementId == Guid.Empty || request.AccountId == Guid.Empty)
        {
            return BadRequest(new { error = "StatementId and AccountId are required." });
        }

        try
        {
            var created = await _batchService.CreateBatchAsync(request.StatementId, request.AccountId, ct);
            return CreatedAtAction(nameof(GetBatchById), new { id = created.Batch.Id }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
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
    /// Retrieves a specific import batch and all of its staged candidate rows.
    /// Access is strictly limited to members of the active household.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ImportBatchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchDetailDto>> GetBatchById(Guid id, CancellationToken ct = default)
    {
        var batch = await _batchService.GetBatchByIdAsync(id, ct);
        if (batch == null)
        {
            return NotFound();
        }

        return Ok(batch);
    }

    /// <summary>
    /// Lists all import batches belonging to the active household, optionally filtered by statement.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ImportBatchSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ImportBatchSummaryDto>>> GetBatches(
        [FromQuery] Guid? statementId,
        CancellationToken ct = default)
    {
        var batches = await _batchService.GetBatchesAsync(statementId, ct);
        return Ok(batches);
    }

    /// <summary>
    /// Updates user resolution and selection on an individual staged transaction row.
    /// Allows correcting ambiguous fields (date, amount, description, direction),
    /// explicitly approving duplicates, or excluding problematic rows.
    /// </summary>
    [HttpPut("{batchId:guid}/rows/{rowId:guid}")]
    [ProducesResponseType(typeof(ImportBatchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchDetailDto>> UpdateStagingRow(
        Guid batchId,
        Guid rowId,
        [FromBody] UpdateStagingRowRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var updated = await _batchService.UpdateStagingRowAsync(batchId, rowId, request, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
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
    /// Bulk updates row selection or exclusion across all or specified rows in an import batch.
    /// </summary>
    [HttpPost("{batchId:guid}/rows/bulk")]
    [ProducesResponseType(typeof(ImportBatchDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchDetailDto>> BulkUpdateRows(
        Guid batchId,
        [FromBody] BulkUpdateRowsRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var updated = await _batchService.BulkUpdateRowsAsync(batchId, request, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
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
    /// Explicitly confirms and commits the import batch.
    /// Revalidates every selected row on the server.
    /// Persists immutable ledger records and marks the batch Committed atomically.
    /// Idempotent: repeated commit requests will not duplicate transactions.
    /// </summary>
    [HttpPost("{batchId:guid}/commit")]
    [ProducesResponseType(typeof(CommitImportBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommitImportBatchResultDto>> CommitBatch(
        Guid batchId,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _batchService.CommitBatchAsync(batchId, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
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
}
