using FinPerf.Api.Domain;
using FinPerf.Api.Models;

namespace FinPerf.Api.Data;

public interface IPerformanceRepository
{
    Task<IReadOnlyList<PortfolioDto>> GetPortfoliosAsync(CancellationToken ct);
    Task<PortfolioDto?> GetPortfolioByIdAsync(int portfolioId, CancellationToken ct);
    Task<DateBounds?> GetDataDateBoundsAsync(int portfolioId, CancellationToken ct);
    Task<IReadOnlyList<DailyPerformancePoint>> GetDailyPerformanceAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct);
}
