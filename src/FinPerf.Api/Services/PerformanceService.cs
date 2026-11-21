using FinPerf.Api.Data;
using FinPerf.Api.Domain;
using FinPerf.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FinPerf.Api.Services;

public sealed record ServiceError(int StatusCode, string Title, string Detail);

public sealed class ServiceResult<T>
{
    public T? Value { get; }
    public ServiceError? Error { get; }
    public bool IsSuccess => Error is null;

    private ServiceResult(T value) => Value = value;
    private ServiceResult(ServiceError error) => Error = error;

    public static ServiceResult<T> Ok(T value) => new(value);
    public static ServiceResult<T> Fail(int statusCode, string title, string detail) =>
        new(new ServiceError(statusCode, title, detail));
}

public sealed class PerformanceService(IPerformanceRepository repository, IMemoryCache cache)
{
    private static readonly TimeSpan PortfolioCacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SeriesCacheTtl = TimeSpan.FromMinutes(2);

    public async Task<IReadOnlyList<PortfolioDto>> GetPortfoliosAsync(CancellationToken ct)
    {
        const string key = "portfolios:all";
        if (cache.TryGetValue(key, out IReadOnlyList<PortfolioDto>? cached) && cached is not null)
        {
            return cached;
        }

        var portfolios = await repository.GetPortfoliosAsync(ct);
        cache.Set(key, portfolios, PortfolioCacheTtl);
        return portfolios;
    }

    public async Task<ServiceResult<PerformanceSummaryResponse>> GetSummaryAsync(
        int portfolioId,
        DateOnly asOf,
        CancellationToken ct)
    {
        var portfolio = await repository.GetPortfolioByIdAsync(portfolioId, ct);
        if (portfolio is null)
        {
            return ServiceResult<PerformanceSummaryResponse>.Fail(
                StatusCodes.Status404NotFound,
                "Portafolio no encontrado",
                $"No existe un portafolio con id {portfolioId}.");
        }

        var bounds = await repository.GetDataDateBoundsAsync(portfolioId, ct);
        if (bounds is null)
        {
            return ServiceResult<PerformanceSummaryResponse>.Fail(
                StatusCodes.Status400BadRequest,
                "Sin datos disponibles",
                $"El portafolio {portfolioId} no tiene registros de rendimiento.");
        }

        if (asOf < bounds.MinDate || asOf > bounds.MaxDate)
        {
            return ServiceResult<PerformanceSummaryResponse>.Fail(
                StatusCodes.Status400BadRequest,
                "Fecha fuera de rango",
                $"La fecha asOf ({asOf:yyyy-MM-dd}) debe estar entre {bounds.MinDate:yyyy-MM-dd} y {bounds.MaxDate:yyyy-MM-dd}.");
        }

        var from = portfolio.InceptionDate < bounds.MinDate ? portfolio.InceptionDate : bounds.MinDate;
        var points = await GetDailySeriesCachedAsync(portfolioId, from, asOf, ct);
        var periods = PerformanceCalculator.CalculateSummary(portfolio.InceptionDate, asOf, points);

        return ServiceResult<PerformanceSummaryResponse>.Ok(
            new PerformanceSummaryResponse(portfolioId, asOf, periods));
    }

    public async Task<ServiceResult<RangePerformanceResponse>> GetRangePerformanceAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var validation = await ValidatePortfolioAndRangeAsync(portfolioId, from, to, ct);
        if (validation is not null)
        {
            return ServiceResult<RangePerformanceResponse>.Fail(
                validation.StatusCode, validation.Title, validation.Detail);
        }

        var points = await GetDailySeriesCachedAsync(portfolioId, from, to, ct);
        var range = PerformanceCalculator.CalculateRange(points);

        return ServiceResult<RangePerformanceResponse>.Ok(
            new RangePerformanceResponse(portfolioId, from, to, range.CumulativeReturn, range.PnL, range.DaysCount));
    }

    public async Task<ServiceResult<MonthlyPerformanceResponse>> GetMonthlyPerformanceAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var validation = await ValidatePortfolioAndRangeAsync(portfolioId, from, to, ct);
        if (validation is not null)
        {
            return ServiceResult<MonthlyPerformanceResponse>.Fail(
                validation.StatusCode, validation.Title, validation.Detail);
        }

        var points = await GetDailySeriesCachedAsync(portfolioId, from, to, ct);
        var months = PerformanceCalculator.CalculateMonthly(points);

        return ServiceResult<MonthlyPerformanceResponse>.Ok(
            new MonthlyPerformanceResponse(portfolioId, from, to, months));
    }

    private async Task<ServiceError?> ValidatePortfolioAndRangeAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        if (from > to)
        {
            return new ServiceError(
                StatusCodes.Status400BadRequest,
                "Rango de fechas inválido",
                $"La fecha 'from' ({from:yyyy-MM-dd}) no puede ser posterior a 'to' ({to:yyyy-MM-dd}).");
        }

        var portfolio = await repository.GetPortfolioByIdAsync(portfolioId, ct);
        if (portfolio is null)
        {
            return new ServiceError(
                StatusCodes.Status404NotFound,
                "Portafolio no encontrado",
                $"No existe un portafolio con id {portfolioId}.");
        }

        var bounds = await repository.GetDataDateBoundsAsync(portfolioId, ct);
        if (bounds is null)
        {
            return new ServiceError(
                StatusCodes.Status400BadRequest,
                "Sin datos disponibles",
                $"El portafolio {portfolioId} no tiene registros de rendimiento.");
        }

        if (from < bounds.MinDate || to > bounds.MaxDate)
        {
            return new ServiceError(
                StatusCodes.Status400BadRequest,
                "Fechas fuera de rango",
                $"El rango solicitado debe estar dentro de {bounds.MinDate:yyyy-MM-dd} y {bounds.MaxDate:yyyy-MM-dd}.");
        }

        return null;
    }

    private async Task<IReadOnlyList<DailyPerformancePoint>> GetDailySeriesCachedAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var key = $"perf:daily:{portfolioId}:{from:yyyyMMdd}:{to:yyyyMMdd}";
        if (cache.TryGetValue(key, out IReadOnlyList<DailyPerformancePoint>? cached) && cached is not null)
        {
            return cached;
        }

        var points = await repository.GetDailyPerformanceAsync(portfolioId, from, to, ct);
        cache.Set(key, points, SeriesCacheTtl);
        return points;
    }
}
