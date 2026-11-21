using FinPerf.Api.Domain;

namespace FinPerf.Api.Models;

public sealed record PortfolioDto(
    int Id,
    string Name,
    DateOnly InceptionDate,
    string BaseCurrency,
    DateOnly? MinDataDate,
    DateOnly? MaxDataDate);

public sealed record DateBounds(DateOnly MinDate, DateOnly MaxDate);

public sealed record PerformanceSummaryResponse(
    int PortfolioId,
    DateOnly AsOf,
    IReadOnlyList<PeriodResult> Periods);

public sealed record RangePerformanceResponse(
    int PortfolioId,
    DateOnly From,
    DateOnly To,
    double? CumulativeReturn,
    decimal PnL,
    int DaysCount);

public sealed record MonthlyPerformanceResponse(
    int PortfolioId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<MonthlyResult> Months);
