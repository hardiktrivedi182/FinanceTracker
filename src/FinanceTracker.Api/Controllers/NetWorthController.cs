using FinanceTracker.Application.NetWorth.Dtos;
using FinanceTracker.Application.NetWorth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceTracker.Api.Controllers;

[ApiController]
[Route("api/household/net-worth")]
[Authorize]
public class NetWorthController : ControllerBase
{
    private readonly INetWorthService _netWorthService;

    public NetWorthController(INetWorthService netWorthService)
    {
        _netWorthService = netWorthService;
    }

    /// <summary>
    /// Computes the household net worth as of a specific date (defaults to today).
    /// Returns asset/liability breakdowns separated by currency, ownership splits, and staleness indicators.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HouseholdNetWorthReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HouseholdNetWorthReportDto>> GetNetWorth(
        [FromQuery] DateOnly? asOf = null,
        [FromQuery] int stalenessThresholdDays = 90,
        CancellationToken ct = default)
    {
        var report = await _netWorthService.GetNetWorthAsync(asOf, stalenessThresholdDays, ct);
        return Ok(report);
    }

    /// <summary>
    /// Compares household net worth between two dates, reporting absolute and percentage changes per currency.
    /// </summary>
    [HttpGet("compare")]
    [ProducesResponseType(typeof(NetWorthComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NetWorthComparisonDto>> CompareNetWorth(
        [FromQuery] DateOnly date1,
        [FromQuery] DateOnly date2,
        [FromQuery] int stalenessThresholdDays = 90,
        CancellationToken ct = default)
    {
        if (date1 > date2)
        {
            return BadRequest(new { error = "date1 must be on or before date2." });
        }

        var comparison = await _netWorthService.CompareNetWorthAsync(date1, date2, stalenessThresholdDays, ct);
        return Ok(comparison);
    }
}
