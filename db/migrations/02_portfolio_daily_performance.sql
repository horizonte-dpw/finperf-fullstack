USE FinPerf;
GO

IF OBJECT_ID('dbo.PortfolioDailyPerformance', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortfolioDailyPerformance (
        PortfolioId      INT           NOT NULL,
        AsOfDate         DATE          NOT NULL,
        MarketValueBegin DECIMAL(19,4) NOT NULL,
        NetFlows         DECIMAL(19,4) NOT NULL,
        PnL              DECIMAL(19,4) NOT NULL,
        MarketValueEnd   DECIMAL(19,4) NOT NULL,
        DailyReturn      FLOAT         NOT NULL,
        LogReturn        FLOAT         NULL,
        UpdatedAtUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_PortfolioDailyPerformance_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PortfolioDailyPerformance PRIMARY KEY CLUSTERED (PortfolioId, AsOfDate),
        CONSTRAINT FK_PortfolioDailyPerformance_Portfolio FOREIGN KEY (PortfolioId) REFERENCES dbo.Portfolio (PortfolioId)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_RefreshPortfolioDailyPerformance
    @PortfolioId INT  = NULL,
    @FromDate    DATE = NULL,
    @ToDate      DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDateTime DATETIME = CASE WHEN @FromDate IS NOT NULL THEN CAST(@FromDate AS DATETIME) END;
    DECLARE @ToDateExclusive DATETIME = CASE WHEN @ToDate IS NOT NULL THEN CAST(DATEADD(DAY, 1, @ToDate) AS DATETIME) END;

    WITH DailyAgg AS (
        SELECT
            PortfolioId,
            CAST(AsOfDate AS DATE) AS AsOfDate,
            SUM(MarketValueBegin)  AS MarketValueBegin,
            SUM(NetFlows)          AS NetFlows,
            SUM(PnL)               AS PnL,
            SUM(MarketValueEnd)    AS MarketValueEnd
        FROM dbo.PositionDaily
        WHERE (@PortfolioId IS NULL OR PortfolioId = @PortfolioId)
          AND (@FromDateTime IS NULL OR AsOfDate >= @FromDateTime)
          AND (@ToDateExclusive IS NULL OR AsOfDate < @ToDateExclusive)
        GROUP BY PortfolioId, CAST(AsOfDate AS DATE)
    ),
    Calculated AS (
        SELECT
            PortfolioId,
            AsOfDate,
            MarketValueBegin,
            NetFlows,
            PnL,
            MarketValueEnd,
            ISNULL(CAST(PnL AS FLOAT) / NULLIF(CAST(MarketValueBegin AS FLOAT), 0.0), 0.0) AS DailyReturn
        FROM DailyAgg
    )
    MERGE dbo.PortfolioDailyPerformance AS target
    USING (
        SELECT
            PortfolioId,
            AsOfDate,
            MarketValueBegin,
            NetFlows,
            PnL,
            MarketValueEnd,
            DailyReturn,
            CASE WHEN 1.0 + DailyReturn > 0.0 THEN LOG(1.0 + DailyReturn) ELSE NULL END AS LogReturn
        FROM Calculated
    ) AS source
        ON target.PortfolioId = source.PortfolioId
       AND target.AsOfDate = source.AsOfDate
    WHEN MATCHED THEN
        UPDATE SET
            MarketValueBegin = source.MarketValueBegin,
            NetFlows         = source.NetFlows,
            PnL              = source.PnL,
            MarketValueEnd   = source.MarketValueEnd,
            DailyReturn      = source.DailyReturn,
            LogReturn        = source.LogReturn,
            UpdatedAtUtc     = SYSUTCDATETIME()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (PortfolioId, AsOfDate, MarketValueBegin, NetFlows, PnL, MarketValueEnd, DailyReturn, LogReturn)
        VALUES (source.PortfolioId, source.AsOfDate, source.MarketValueBegin, source.NetFlows, source.PnL, source.MarketValueEnd, source.DailyReturn, source.LogReturn)
    WHEN NOT MATCHED BY SOURCE
         AND (@PortfolioId IS NULL OR target.PortfolioId = @PortfolioId)
         AND (@FromDate IS NULL OR target.AsOfDate >= @FromDate)
         AND (@ToDate IS NULL OR target.AsOfDate <= @ToDate)
    THEN DELETE;
END;
GO

EXEC dbo.usp_RefreshPortfolioDailyPerformance;
GO
