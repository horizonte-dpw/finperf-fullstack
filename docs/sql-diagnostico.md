# Diagnóstico y optimización SQL Server

## 1. Medición base (antes)

Consulta evaluada sobre `FinPerf` (5.218.000 filas en `dbo.PositionDaily`, ~409 MB reservados, rango `2015-01-01` a `2024-12-31`):

```sql
SET STATISTICS IO, TIME ON;
EXEC dbo.usp_GetPerformance @PortfolioId = 7, @Year = 2023;
```

### Resultados obtenidos

| Consulta dentro del SP | Tabla / Recurso | Scans | Logical reads | CPU (ms) | Elapsed (ms) |
|---|---|---:|---:|---:|---:|
| Retorno y P&L mensual | `PositionDaily` | 27 | 154.526 | — | — |
| Retorno y P&L mensual | `Worktable` (Index Spool) | 780 | 16.145.544 | 94.955 | 21.537 |
| Acumulado anual (YTD) | `PositionDaily` | 13 | 51.816 | 333 | 61 |
| **Total SP** | — | — | **16.351.886** | **95.525** | **21.798** |

### Operadores clave del plan de ejecución base
El plan de ejecución estimado completo está adjunto en `docs/plan-base.sqlplan` (obtenido con `SET SHOWPLAN_XML ON` sobre `EXEC dbo.usp_GetPerformance @PortfolioId = 7, @Year = 2023;`). Sus principales cuellos de botella son:
- `Clustered Index Scan` completo sobre `PK__Position__...` (identidad `PositionDailyId`) repetido en la subconsulta de días distintos, en la subconsulta correlacionada de `PnLMes` y en la consulta de YTD.
- `Index Spool` sobre un `Clustered Index Scan` de toda la tabla para resolver `dbo.fn_PortfolioDailyReturn` (aun con scalar UDF inlining en SQL Server 2022, el predicado no SARGable impide un seek directo).
- `Filter` con conversión `CONVERT(varchar(10), AsOfDate, 120) = CONVERT(varchar(10), @Date, 120)` evaluada fila a fila.

---

## 2. Problemas encontrados

### 2.1 Rendimiento y modelo de datos
1. **Falta de índice por `(PortfolioId, AsOfDate)`:** `dbo.PositionDaily` solo tiene la clave primaria clustered en `PositionDailyId` (`BIGINT IDENTITY`). Cualquier filtro por portafolio y fecha obliga a recorrer los 5,2 millones de filas.
2. **Función escalar `dbo.fn_PortfolioDailyReturn` por día:** se invoca una vez por cada día hábil del año (~260 veces), generando 780 scans y 16,1 millones de lecturas lógicas sobre el `Worktable` del spool.
3. **Predicados no SARGables:** `CONVERT(VARCHAR(10), AsOfDate, 120) = ...`, `YEAR(AsOfDate) = @Year` y `MONTH(AsOfDate) = d.Mes` impiden utilizar búsquedas por índice incluso existiendo un índice sobre `AsOfDate`.
4. **Subconsulta correlacionada innecesaria para `PnLMes`:** vuelve a recorrer `PositionDaily` por cada mes en lugar de agregar en un solo paso.
5. **Tipo de dato `AsOfDate DATETIME`:** ocupa 8 bytes cuando el dato es estrictamente diario (`DATE` ocupa 3 bytes y evita comparaciones con componente horario).
6. **Ausencia de claves foráneas y `SET NOCOUNT ON`:** no existen FKs hacia `Portfolio` e `Instrument` ni `SET NOCOUNT ON` en el procedimiento.

### 2.2 Corrección del cálculo financiero
1. **Suma de retornos en vez de composición (`RetornoMes`):** `SUM(dbo.fn_PortfolioDailyReturn(...))` suma los retornos diarios del mes. El retorno acumulado debe componerse como $\prod_{i=1}^{n}(1 + r_i) - 1$.
2. **Cálculo de `RetornoYTD` doblemente erróneo:** `SUM(DailyReturn)` sobre `PositionDaily` suma los retornos individuales de los 100 instrumentos sin ponderarlos por `MarketValueBegin` y además suma a través de los 260 días en vez de componer. Para el portafolio 7 en 2023 devuelve **`+613,14%`** (`6.13142`), cuando el retorno anual real compuesto es **`+5,33%`** (`0.053264`) para el mismo P&L de `$2.856.350,87`.
3. **Truncamiento de precisión en `fn_PortfolioDailyReturn`:** al dividir `SUM(PnL) / SUM(MarketValueBegin)` siendo ambos `DECIMAL(19,4)`, SQL Server produce un `DECIMAL(38,6)` que trunca a 6 decimales antes de asignarlo a `@r FLOAT`. Convertir a `FLOAT` antes de dividir evita esa pérdida de precisión.
4. **Sin protección ante división por cero:** `SUM(PnL) / SUM(MarketValueBegin)` falla si `SUM(MarketValueBegin) = 0`. Corresponde proteger con `NULLIF(..., 0)`.
5. **Granularidad fija por año:** el SP actual solo recibe `@Year`, sin soportar una fecha de corte `@AsOfDate` para MTD, QTD, YTD e ITD ni rangos arbitrarios `@FromDate`–`@ToDate`.

---

## 3. Cambios propuestos y aplicados

Todos los scripts están en `db/migrations/` sin alterar `db/01_schema_y_datos.sql`:

1. **`db/migrations/01_indexes_and_constraints.sql`**
   - Claves foráneas `FK_PositionDaily_Portfolio` y `FK_PositionDaily_Instrument`.
   - Índice nonclustered de cobertura `IX_PositionDaily_Portfolio_AsOfDate (PortfolioId, AsOfDate) INCLUDE (MarketValueBegin, NetFlows, PnL, MarketValueEnd)` para acelerar recálculos incrementales y consultas sobre el detalle por instrumento.

2. **`db/migrations/02_portfolio_daily_performance.sql`**
   - Tabla pre-agregada `dbo.PortfolioDailyPerformance` con PK clustered `(PortfolioId, AsOfDate)` (`AsOfDate DATE`), almacenando `MarketValueBegin`, `NetFlows`, `PnL`, `MarketValueEnd`, `DailyReturn` (ponderado por portafolio) y `LogReturn = LOG(1 + DailyReturn)`.
   - Reduce el universo de lectura de 5.218.000 filas (409 MB) a 52.180 filas (4,0 MB): factor 100x.
   - Procedimiento idempotente `dbo.usp_RefreshPortfolioDailyPerformance (@PortfolioId, @FromDate, @ToDate)` con `MERGE` y predicados SARGables para carga inicial y recálculo incremental.

3. **`db/migrations/03_performance_queries.sql`**
   - `dbo.usp_GetPortfolioPerformanceSummary @PortfolioId, @AsOfDate`: calcula MTD, QTD, YTD e ITD usando `EXP(SUM(LogReturn)) - 1` y `SUM(PnL)`.
   - `dbo.usp_GetPortfolioPerformanceRange @PortfolioId, @FromDate, @ToDate`: retorno compuesto y P&L de un rango.
   - `dbo.usp_GetPortfolioPerformanceMonthly @PortfolioId, @FromDate, @ToDate`: retorno compuesto y P&L agrupados por año y mes.

---

## 4. Medición después de los cambios

### 4.1 Procedimientos SQL optimizados (`db/migrations/03_performance_queries.sql`)

Comandos ejecutados para reproducir la medición:

```sql
SET STATISTICS IO, TIME ON;
EXEC dbo.usp_GetPortfolioPerformanceSummary @PortfolioId = 7, @AsOfDate = '2023-08-15';
EXEC dbo.usp_GetPortfolioPerformanceRange   @PortfolioId = 7, @FromDate = '2023-01-01', @ToDate = '2023-12-31';
EXEC dbo.usp_GetPortfolioPerformanceMonthly @PortfolioId = 7, @FromDate = '2023-01-01', @ToDate = '2023-12-31';
```

| Procedimiento | Parámetros | Scans | Logical reads | CPU (ms) | Elapsed (ms) |
|---|---|---:|---:|---:|---:|
| `usp_GetPortfolioPerformanceSummary` | `@PortfolioId = 7, @AsOfDate = '2023-08-15'` | 4 | 37 (`35` + `2`) | 1 | 1 |
| `usp_GetPortfolioPerformanceRange` | `@PortfolioId = 7, @FromDate = '2023-01-01', @ToDate = '2023-12-31'` | 1 | 6 | 0 | 0 |
| `usp_GetPortfolioPerformanceMonthly` | `@PortfolioId = 7, @FromDate = '2023-01-01', @ToDate = '2023-12-31'` | 1 | 6 | 0 | 0 |

### 4.2 Ruta de lectura utilizada por la API (`SqlPerformanceRepository` + composición en C#)

La API consulta la serie diaria pre-agregada de `dbo.PortfolioDailyPerformance` y ejecuta la composición acumulada en `PerformanceCalculator` (C#) para mantener aislada y testeada la lógica de cálculo:

```sql
SET STATISTICS IO, TIME ON;
SELECT AsOfDate, DailyReturn, PnL
FROM dbo.PortfolioDailyPerformance
WHERE PortfolioId = 7 AND AsOfDate >= '2015-01-01' AND AsOfDate <= '2023-08-15'
ORDER BY AsOfDate;

SELECT AsOfDate, DailyReturn, PnL
FROM dbo.PortfolioDailyPerformance
WHERE PortfolioId = 7 AND AsOfDate >= '2023-01-01' AND AsOfDate <= '2023-12-31'
ORDER BY AsOfDate;
```

| Consulta API | Filas | Scans | Logical reads | CPU (ms) | Elapsed (ms) |
|---|---:|---:|---:|---:|---:|
| Serie diaria ITD (`2015-01-01`–`2023-08-15`) | 2.249 | 1 | 24 | 0 | 0 |
| Serie diaria anual (`2023-01-01`–`2023-12-31`) | 260 | 1 | 6 | 0 | 0 |

### Comparativa antes vs después (Portafolio 7, año 2023)

| Métrica | Antes (`usp_GetPerformance`) | Después (SPs SQL / Lectura API) | Mejora |
|---|---:|---:|---:|
| Logical reads | 16.351.886 | 12 (SPs) / 6 (serie diaria API) | > 1.300.000x |
| Tiempo de ejecución SQL (ms) | 21.798 ms | < 2 ms | > 10.000x |
| Retorno anual 2023 | +613,14% (incorrecto) | +5,33% (`0,053264`) | Corregido |
| P&L anual 2023 | `$2.856.350,87` | `$2.856.350,87` | Consistente |

Todas las consultas operan muy por debajo del objetivo orientativo de 200 ms.

### Nota sobre retornos diarios extremos (`r <= -100%`)
En el conjunto sintético los retornos diarios oscilan entre `-1,98%` y `+2,02%`, por lo que `1 + DailyReturn` siempre es estrictamente positivo. Para mantener consistencia entre SQL y C# ante un eventual día con pérdida total (`DailyReturn <= -1`), `LogReturn` se guarda como `NULL` cuando `1 + DailyReturn <= 0` y los procedimientos SQL devuelven `-1.0` (`-100%`) al detectar `MIN(1.0 + DailyReturn) <= 0.0`, replicando el comportamiento de la multiplicación directa en C#.

---

## 5. Mantenimiento de estructuras pre-calculadas

La tabla `dbo.PortfolioDailyPerformance` tiene granularidad diaria por portafolio, por lo que una modificación en un día solo afecta a la fila `(PortfolioId, AsOfDate)` correspondiente (no arrastra estado acumulado hacia días posteriores, ya que la composición se calcula al consultar sobre las filas diarias).

- **Carga diaria normal:** al finalizar el proceso ETL que inserta los registros del día `@LoadDate` en `dbo.PositionDaily`, se ejecuta `EXEC dbo.usp_RefreshPortfolioDailyPerformance @FromDate = @LoadDate, @ToDate = @LoadDate;` dentro del mismo pipeline (o paso posterior del job). Gracias al índice `IX_PositionDaily_Portfolio_AsOfDate`, el recálculo de un día completo para los 20 portafolios lee solo ~2.000 filas.
- **Corrección o reproceso de días pasados:** se invoca el mismo procedimiento acotado al portafolio y rango corregido (`@PortfolioId`, `@FromDate`, `@ToDate`). Al usar `MERGE` con cláusula `WHEN NOT MATCHED BY SOURCE ... THEN DELETE`, el procedimiento es idempotente: inserta días nuevos, actualiza valores recalculados y elimina fechas si se revirtió una carga completa.
- **Invalidación de caché en la API:** al completar un recálculo, se invalida la entrada de caché asociada al `PortfolioId` afectado (o se versiona la clave de caché usando el `MAX(UpdatedAtUtc)` del portafolio).
