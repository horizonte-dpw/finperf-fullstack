using FinPerf.Api.Domain;
using Xunit;

namespace FinPerf.Tests;

public sealed class PerformanceCalculatorTests
{
    [Fact]
    public void CalculateCumulativeReturn_Plus10AndMinus10_ReturnsMinus1Percent()
    {
        var result = PerformanceCalculator.CalculateCumulativeReturn([0.10, -0.10]);

        Assert.Equal(-0.01, result, precision: 10);
    }

    [Fact]
    public void CalculateCumulativeReturn_Plus10Minus10Plus5_Returns3Point95Percent()
    {
        var result = PerformanceCalculator.CalculateCumulativeReturn([0.10, -0.10, 0.05]);

        Assert.Equal(0.0395, result, precision: 10);
    }

    [Fact]
    public void CalculateDailyReturn_WeightedInstrumentsAAndB_Returns0Point4545Percent()
    {
        var positions = new[]
        {
            new PositionSnapshot(MarketValueBegin: 1_000_000m, PnL: 10_000m),
            new PositionSnapshot(MarketValueBegin: 100_000m, PnL: -5_000m)
        };

        var result = PerformanceCalculator.CalculateDailyReturn(positions);

        Assert.Equal(5_000d / 1_100_000d, result, precision: 10);
        Assert.Equal(0.004545, Math.Round(result, 6));
    }

    [Fact]
    public void CalculateDailyReturn_ZeroMarketValueBegin_ReturnsZero()
    {
        var result = PerformanceCalculator.CalculateDailyReturn([new PositionSnapshot(0m, 0m)]);

        Assert.Equal(0d, result);
    }

    [Fact]
    public void CalculateSummary_AsOf20230815_ComputesMtdQtdYtdItdBoundaries()
    {
        var points = new List<DailyPerformancePoint>
        {
            new(new DateOnly(2022, 12, 30), 0.10, 100m),
            new(new DateOnly(2023, 02, 10), 0.05, 50m),
            new(new DateOnly(2023, 07, 15), -0.10, -80m),
            new(new DateOnly(2023, 08, 01), 0.10, 90m),
            new(new DateOnly(2023, 08, 15), -0.10, -70m)
        };

        var summary = PerformanceCalculator.CalculateSummary(
            new DateOnly(2015, 01, 01),
            new DateOnly(2023, 08, 15),
            points);

        var mtd = summary.Single(x => x.Period == "MTD");
        var qtd = summary.Single(x => x.Period == "QTD");
        var ytd = summary.Single(x => x.Period == "YTD");
        var itd = summary.Single(x => x.Period == "ITD");

        Assert.Equal(new DateOnly(2023, 08, 01), mtd.From);
        Assert.Equal(-0.01, mtd.CumulativeReturn!.Value, precision: 10);
        Assert.Equal(20m, mtd.PnL);

        Assert.Equal(new DateOnly(2023, 07, 01), qtd.From);
        Assert.Equal(0.9 * 1.1 * 0.9 - 1d, qtd.CumulativeReturn!.Value, precision: 10);
        Assert.Equal(-60m, qtd.PnL);

        Assert.Equal(new DateOnly(2023, 01, 01), ytd.From);
        Assert.Equal(1.05 * 0.9 * 1.1 * 0.9 - 1d, ytd.CumulativeReturn!.Value, precision: 10);
        Assert.Equal(-10m, ytd.PnL);

        Assert.Equal(new DateOnly(2015, 01, 01), itd.From);
        Assert.Equal(1.1 * 1.05 * 0.9 * 1.1 * 0.9 - 1d, itd.CumulativeReturn!.Value, precision: 10);
        Assert.Equal(90m, itd.PnL);
    }

    [Fact]
    public void CalculateMonthly_GroupsAndCompoundsByYearAndMonth()
    {
        var points = new[]
        {
            new DailyPerformancePoint(new DateOnly(2023, 01, 02), 0.10, 100m),
            new DailyPerformancePoint(new DateOnly(2023, 01, 03), -0.10, -90m),
            new DailyPerformancePoint(new DateOnly(2023, 02, 01), 0.05, 50m)
        };

        var monthly = PerformanceCalculator.CalculateMonthly(points);

        Assert.Equal(2, monthly.Count);
        Assert.Equal(2023, monthly[0].Year);
        Assert.Equal(1, monthly[0].Month);
        Assert.Equal(-0.01, monthly[0].CumulativeReturn, precision: 10);
        Assert.Equal(10m, monthly[0].PnL);

        Assert.Equal(2023, monthly[1].Year);
        Assert.Equal(2, monthly[1].Month);
        Assert.Equal(0.05, monthly[1].CumulativeReturn, precision: 10);
        Assert.Equal(50m, monthly[1].PnL);
    }
}
