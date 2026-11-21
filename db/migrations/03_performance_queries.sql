USE FinPerf;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetPortfolioPerformanceSummary
    @PortfolioId INT,
    @AsOfDate    DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @InceptionDate DATE;
    SELECT @InceptionDate = InceptionDate
    FROM dbo.Portfolio
    WHERE PortfolioId = @PortfolioId;

    IF @InceptionDate IS NULL
        RETURN;

    DECLARE @MtdStart DATE = DATEFROMPARTS(YEAR(@AsOfDate), MONTH(@AsOfDate), 1);
    DECLARE @QtdStart DATE = DATEFROMPARTS(YEAR(@AsOfDate), ((MONTH(@AsOfDate) - 1) / 3) * 3 + 1, 1);
    DECLARE @YtdStart DATE = DATEFROMPARTS(YEAR(@AsOfDate), 1, 1);

    SELECT
        p.Period,
        p.FromDate,
        @AsOfDate AS ToDate,
        CASE WHEN COUNT(d.AsOfDate) > 0 THEN EXP(SUM(d.LogReturn)) - 1.0 ELSE NULL END AS CumulativeReturn,
        SUM(d.PnL) AS PnL,
        COUNT(d.AsOfDate) AS DaysCount
    FROM (VALUES
        ('MTD', @MtdStart, 1),
        ('QTD', @QtdStart, 2),
        ('YTD', @YtdStart, 3),
        ('ITD', @InceptionDate, 4)
    ) AS p(Period, FromDate, SortOrder)
    LEFT JOIN dbo.PortfolioDailyPerformance d
        ON d.PortfolioId = @PortfolioId
       AND d.AsOfDate >= p.FromDate
       AND d.AsOfDate <= @AsOfDate
    GROUP BY p.Period, p.FromDate, p.SortOrder
    ORDER BY p.SortOrder;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetPortfolioPerformanceRange
    @PortfolioId INT,
    @FromDate    DATE,
    @ToDate      DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        @FromDate AS FromDate,
        @ToDate   AS ToDate,
        CASE WHEN COUNT(*) > 0 THEN EXP(SUM(LogReturn)) - 1.0 ELSE NULL END AS CumulativeReturn,
        ISNULL(SUM(PnL), 0) AS PnL,
        COUNT(*) AS DaysCount
    FROM dbo.PortfolioDailyPerformance
    WHERE PortfolioId = @PortfolioId
      AND AsOfDate >= @FromDate
      AND AsOfDate <= @ToDate;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetPortfolioPerformanceMonthly
    @PortfolioId INT,
    @FromDate    DATE,
    @ToDate      DATE
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        YEAR(AsOfDate)          AS [Year],
        MONTH(AsOfDate)         AS [Month],
        EXP(SUM(LogReturn)) - 1.0 AS MonthlyReturn,
        SUM(PnL)                AS PnL,
        COUNT(*)                AS DaysCount
    FROM dbo.PortfolioDailyPerformance
    WHERE PortfolioId = @PortfolioId
      AND AsOfDate >= @FromDate
      AND AsOfDate <= @ToDate
    GROUP BY YEAR(AsOfDate), MONTH(AsOfDate)
    ORDER BY [Year], [Month];
END;
GO
