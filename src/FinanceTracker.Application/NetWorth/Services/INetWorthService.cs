using FinanceTracker.Application.NetWorth.Dtos;

namespace FinanceTracker.Application.NetWorth.Services;

public interface INetWorthService
{
    Task<HouseholdNetWorthReportDto> GetNetWorthAsync(DateOnly? asOfDate = null, int stalenessThresholdDays = 90, CancellationToken ct = default);
    Task<NetWorthComparisonDto> CompareNetWorthAsync(DateOnly date1, DateOnly date2, int stalenessThresholdDays = 90, CancellationToken ct = default);
}
