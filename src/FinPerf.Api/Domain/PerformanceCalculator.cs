namespace FinPerf.Api.Domain;

public sealed record PositionSnapshot(decimal MarketValueBegin, decimal PnL);

public sealed record DailyPerformancePoint(DateOnly AsOfDate, double DailyReturn, decimal PnL);

public sealed record PeriodResult(
    string Period,
    DateOnly From,
    DateOnly To,
    double? CumulativeReturn,
    decimal PnL,
    int DaysCount);

public sealed record RangeResult(double? CumulativeReturn, decimal PnL, int DaysCount);

public sealed record MonthlyResult(int Year, int Month, double CumulativeReturn, decimal PnL, int DaysCount);

public static class PerformanceCalculator
{
    public static double CalculateDailyReturn(IEnumerable<PositionSnapshot> positions)
    {
        decimal totalPnL = 0m;
        decimal totalMarketValueBegin = 0m;

        foreach (var p in positions)
        {
            totalPnL += p.PnL;
            totalMarketValueBegin += p.MarketValueBegin;
        }

        if (totalMarketValueBegin == 0m)
        {
            return 0d;
        }

        return (double)(totalPnL / totalMarketValueBegin);
    }

    public static double CalculateCumulativeReturn(IEnumerable<double> dailyReturns)
    {
        double factor = 1d;
        bool hasAny = false;

        foreach (var r in dailyReturns)
        {
            factor *= 1d + r;
            hasAny = true;
        }

        return hasAny ? factor - 1d : 0d;
    }

    public static RangeResult CalculateRange(IReadOnlyCollection<DailyPerformancePoint> points)
    {
        if (points.Count == 0)
        {
            return new RangeResult(null, 0m, 0);
        }

        double factor = 1d;
        decimal totalPnL = 0m;

        foreach (var point in points)
        {
            factor *= 1d + point.DailyReturn;
            totalPnL += point.PnL;
        }

        return new RangeResult(factor - 1d, totalPnL, points.Count);
    }

    public static IReadOnlyList<MonthlyResult> CalculateMonthly(IEnumerable<DailyPerformancePoint> points)
    {
        return points
            .GroupBy(p => (p.AsOfDate.Year, p.AsOfDate.Month))
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                double factor = 1d;
                decimal pnl = 0m;
                int count = 0;

                foreach (var item in g)
                {
                    factor *= 1d + item.DailyReturn;
                    pnl += item.PnL;
                    count++;
                }

                return new MonthlyResult(g.Key.Year, g.Key.Month, factor - 1d, pnl, count);
            })
            .ToList();
    }

    public static IReadOnlyList<PeriodResult> CalculateSummary(
        DateOnly inceptionDate,
        DateOnly asOf,
        IReadOnlyList<DailyPerformancePoint> pointsUpToAsOf)
    {
        var mtdStart = new DateOnly(asOf.Year, asOf.Month, 1);
        var qtdMonth = ((asOf.Month - 1) / 3) * 3 + 1;
        var qtdStart = new DateOnly(asOf.Year, qtdMonth, 1);
        var ytdStart = new DateOnly(asOf.Year, 1, 1);

        return
        [
            BuildPeriod("MTD", mtdStart, asOf, pointsUpToAsOf),
            BuildPeriod("QTD", qtdStart, asOf, pointsUpToAsOf),
            BuildPeriod("YTD", ytdStart, asOf, pointsUpToAsOf),
            BuildPeriod("ITD", inceptionDate, asOf, pointsUpToAsOf)
        ];
    }

    private static PeriodResult BuildPeriod(
        string period,
        DateOnly from,
        DateOnly to,
        IReadOnlyList<DailyPerformancePoint> allPoints)
    {
        double factor = 1d;
        decimal pnl = 0m;
        int count = 0;

        foreach (var point in allPoints)
        {
            if (point.AsOfDate < from || point.AsOfDate > to)
            {
                continue;
            }

            factor *= 1d + point.DailyReturn;
            pnl += point.PnL;
            count++;
        }

        double? cumulativeReturn = count > 0 ? factor - 1d : null;
        return new PeriodResult(period, from, to, cumulativeReturn, pnl, count);
    }
}
