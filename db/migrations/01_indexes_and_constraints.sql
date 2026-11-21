USE FinPerf;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PositionDaily_Portfolio')
BEGIN
    ALTER TABLE dbo.PositionDaily
    ADD CONSTRAINT FK_PositionDaily_Portfolio
        FOREIGN KEY (PortfolioId) REFERENCES dbo.Portfolio (PortfolioId);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PositionDaily_Instrument')
BEGIN
    ALTER TABLE dbo.PositionDaily
    ADD CONSTRAINT FK_PositionDaily_Instrument
        FOREIGN KEY (InstrumentId) REFERENCES dbo.Instrument (InstrumentId);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_PositionDaily_Portfolio_AsOfDate'
      AND object_id = OBJECT_ID('dbo.PositionDaily')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_PositionDaily_Portfolio_AsOfDate
        ON dbo.PositionDaily (PortfolioId, AsOfDate)
        INCLUDE (MarketValueBegin, NetFlows, PnL, MarketValueEnd);
END;
GO
