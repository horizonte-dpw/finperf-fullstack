using Dapper;
using FinPerf.Api.Domain;
using FinPerf.Api.Models;
using Microsoft.Data.SqlClient;

namespace FinPerf.Api.Data;

public sealed class SqlPerformanceRepository(IConfiguration configuration) : IPerformanceRepository
{
    private readonly string _connectionString =
        configuration.GetConnectionString("FinPerf")
        ?? throw new InvalidOperationException("Connection string 'FinPerf' is not configured.");

    public async Task<IReadOnlyList<PortfolioDto>> GetPortfoliosAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT
                p.PortfolioId AS Id,
                p.Name,
                p.InceptionDate,
                p.BaseCurrency,
                b.MinDataDate,
                b.MaxDataDate
            FROM dbo.Portfolio p
            OUTER APPLY (
                SELECT MIN(AsOfDate) AS MinDataDate, MAX(AsOfDate) AS MaxDataDate
                FROM dbo.PortfolioDailyPerformance d
                WHERE d.PortfolioId = p.PortfolioId
            ) b
            ORDER BY p.PortfolioId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        var command = new CommandDefinition(sql, cancellationToken: ct);
        var rows = await conn.QueryAsync<PortfolioDto>(command);
        return rows.AsList();
    }

    public async Task<PortfolioDto?> GetPortfolioByIdAsync(int portfolioId, CancellationToken ct)
    {
        const string sql = """
            SELECT
                p.PortfolioId AS Id,
                p.Name,
                p.InceptionDate,
                p.BaseCurrency,
                b.MinDataDate,
                b.MaxDataDate
            FROM dbo.Portfolio p
            OUTER APPLY (
                SELECT MIN(AsOfDate) AS MinDataDate, MAX(AsOfDate) AS MaxDataDate
                FROM dbo.PortfolioDailyPerformance d
                WHERE d.PortfolioId = p.PortfolioId
            ) b
            WHERE p.PortfolioId = @PortfolioId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        var command = new CommandDefinition(sql, new { PortfolioId = portfolioId }, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<PortfolioDto>(command);
    }

    public async Task<DateBounds?> GetDataDateBoundsAsync(int portfolioId, CancellationToken ct)
    {
        const string sql = """
            SELECT MIN(AsOfDate) AS MinDate, MAX(AsOfDate) AS MaxDate
            FROM dbo.PortfolioDailyPerformance
            WHERE PortfolioId = @PortfolioId;
            """;

        await using var conn = new SqlConnection(_connectionString);
        var command = new CommandDefinition(sql, new { PortfolioId = portfolioId }, cancellationToken: ct);
        var row = await conn.QuerySingleOrDefaultAsync<(DateOnly? MinDate, DateOnly? MaxDate)>(command);

        if (row.MinDate is null || row.MaxDate is null)
        {
            return null;
        }

        return new DateBounds(row.MinDate.Value, row.MaxDate.Value);
    }

    public async Task<IReadOnlyList<DailyPerformancePoint>> GetDailyPerformanceAsync(
        int portfolioId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        const string sql = """
            SELECT AsOfDate, DailyReturn, PnL
            FROM dbo.PortfolioDailyPerformance
            WHERE PortfolioId = @PortfolioId
              AND AsOfDate >= @FromDate
              AND AsOfDate <= @ToDate
            ORDER BY AsOfDate;
            """;

        await using var conn = new SqlConnection(_connectionString);
        var command = new CommandDefinition(
            sql,
            new { PortfolioId = portfolioId, FromDate = from, ToDate = to },
            cancellationToken: ct);

        var rows = await conn.QueryAsync<DailyPerformancePoint>(command);
        return rows.AsList();
    }
}
