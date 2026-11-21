# Prueba Técnica — Rendimiento de Portafolios (.NET 8 + SQL Server + React)

Solución para el diagnóstico y optimización de consultas de rendimiento de portafolios sobre ~5,2 millones de filas en SQL Server 2022, expuesta mediante una API en ASP.NET Core 8 y consumida por una pantalla en React.

## Estructura del repositorio

- `db/01_schema_y_datos.sql` y `db/02_procedimiento_actual.sql`: scripts base entregados (sin modificar).
- `db/migrations/`: scripts incrementales de índices, tabla pre-agregada `dbo.PortfolioDailyPerformance`, procedimiento de recálculo y consultas optimizadas.
- `docs/sql-diagnostico.md`: mediciones antes/después (`SET STATISTICS IO, TIME ON`), análisis del plan de ejecución, correcciones de cálculo y estrategia de mantenimiento.
- `src/FinPerf.Api/`: Web API en .NET 8 con Dapper.
- `src/FinPerf.Tests/`: pruebas unitarias (xUnit) sobre la lógica de cálculo y pruebas de integración (`WebApplicationFactory`).
- `src/finperf-web/`: pantalla de consulta en React + Vite + TypeScript.

---

## Cómo ejecutar la solución

### Requisitos
- Docker y Docker Compose
- .NET 8 SDK
- Node.js 20+ y npm

### 1. Levantar SQL Server y cargar datos base

```bash
docker compose up -d
```

Si el puerto `1433` ya está ocupado en tu máquina, puedes sobrescribirlo con `SQL_PORT`:

```bash
SQL_PORT=1435 docker compose up -d
```

Espera a que el contenedor `db-init` termine la carga inicial (~5,2 millones de filas). Puedes verificarlo con:

```bash
docker compose logs -f db-init
```

### 2. Aplicar migraciones SQL

```bash
docker exec -i finperf-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'FinPerf_Dev_2026!' -C -b < db/migrations/01_indexes_and_constraints.sql
docker exec -i finperf-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'FinPerf_Dev_2026!' -C -b < db/migrations/02_portfolio_daily_performance.sql
docker exec -i finperf-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'FinPerf_Dev_2026!' -C -b < db/migrations/03_performance_queries.sql
```

### 3. Configurar cadena de conexión y ejecutar la API

La cadena de conexión no se guarda en el código. Puedes configurarla con `dotnet user-secrets` o mediante variable de entorno:

Opción A (`user-secrets`):

```bash
dotnet user-secrets --project src/FinPerf.Api set "ConnectionStrings:FinPerf" "Server=localhost,1433;Database=FinPerf;User Id=sa;Password=FinPerf_Dev_2026!;TrustServerCertificate=True;"
dotnet run --project src/FinPerf.Api
```

Opción B (variable de entorno):

```bash
ConnectionStrings__FinPerf="Server=localhost,1433;Database=FinPerf;User Id=sa;Password=FinPerf_Dev_2026!;TrustServerCertificate=True;" \
  dotnet run --project src/FinPerf.Api
```

La API queda disponible en `http://localhost:5080` (Swagger en `http://localhost:5080/swagger` en modo Development).

### 4. Ejecutar pruebas automatizadas

```bash
dotnet test FinPerf.sln
```

### 5. Ejecutar la pantalla React

```bash
cd src/finperf-web
npm install
npm run dev
```

Abrir `http://localhost:5173`. Si la API corre en otra URL, definir `VITE_API_BASE_URL` antes de iniciar Vite.

---

## Decisiones técnicas

- **Dapper en lugar de EF Core:** las consultas son de solo lectura y de perfil analítico. Dapper permite controlar el SQL exacto, evitar el overhead de change tracking y pasar `CancellationToken` mediante `CommandDefinition` con mínima complejidad.
- **Pre-agregación a nivel diario (`dbo.PortfolioDailyPerformance`):** reduce 5,2 millones de filas (por instrumento) a ~52 mil filas (por portafolio/día). Con una clave primaria clustered `(PortfolioId, AsOfDate)`, cualquier consulta MTD/QTD/YTD/ITD o mensual se resuelve en < 5 ms sin necesidad de mantener tablas separadas por mes/trimestre/año.
- **Cálculo compuesto aislado en `PerformanceCalculator`:** se obtienen los retornos diarios ya agregados por portafolio desde `PortfolioDailyPerformance` (máximo ~2.609 filas para 10 años) y la composición $\prod(1 + r_i) - 1$ se ejecuta en C# con pruebas unitarias sobre los casos de control. Adicionalmente, `db/migrations/03_performance_queries.sql` provee los procedimientos equivalentes usando `EXP(SUM(LogReturn)) - 1` con resultados consistentes.
- **Caché en memoria (`IMemoryCache`):** se cachea el listado de portafolios (TTL 5 minutos) y las series diarias por `(portfolioId, from, to)` (TTL 2 minutos). En un entorno productivo con múltiples instancias, se migraría a caché distribuida (Redis) invalidada al finalizar `usp_RefreshPortfolioDailyPerformance` o versionando la clave con `MAX(UpdatedAtUtc)` por portafolio.
- **React + Vite + TypeScript:** alineado con la migración de Razor a React indicada en el contexto. Uso de `Intl.NumberFormat('es-CL')` para porcentajes y montos en CLP, y `AbortController` para cancelar solicitudes previas al cambiar filtros.

---

## Supuestos relevantes

1. **P&L y aportes/retiros:** los datos sintéticos ya traen `PnL` descontando `NetFlows`, por lo que el retorno diario del portafolio se calcula como `SUM(PnL) / SUM(MarketValueBegin)` protegiendo contra división por cero (`NULLIF`).
2. **Días sin cotización (fines de semana / feriados):** si la fecha de corte `asOf` cae en un día no hábil dentro del rango con datos (`2015-01-01` a `2024-12-31`), se consideran todos los días hábiles hasta esa fecha inclusive. Si un rango válido no contiene días hábiles, `daysCount` es `0` y `cumulativeReturn` es `null`.
3. **Definición de ITD:** parte desde `Portfolio.InceptionDate` hasta `asOf`.
4. **Rango válido de fechas:** se valida contra el rango real disponible para el portafolio (`MIN(AsOfDate)` y `MAX(AsOfDate)` en `PortfolioDailyPerformance`), retornando `400 Bad Request` con `ProblemDetails` fuera de ese rango o cuando `from > to`, y `404 Not Found` si el portafolio no existe.
5. **Moneda:** todos los portafolios de prueba operan en `CLP`; el formateo en pantalla aplica formato chileno (`es-CL`).

---

## Qué quedó pendiente / mejoras con más tiempo

- **Tests de integración contra SQL Server real** (por ejemplo con Testcontainers) además de los tests actuales con `WebApplicationFactory` y repositorio en memoria.
- **Endpoint o mecanismo explícito de invalidación de caché** conectado al proceso de carga diaria (o caché distribuida en Redis con clave versionada por `UpdatedAtUtc`).
- **Migraciones versionadas automáticamente** (DbUp o FluentMigrator) integradas en CI/CD.
- **Cobertura de tests en frontend** (Vitest + React Testing Library) y selector opcional de rango personalizado en pantalla para consumir directamente `GET /api/portfolios/{id}/performance`.
- **Observabilidad:** métricas de latencia SQL/HTTP y health checks (`/health`) hacia SQL Server.

---

## Tiempo invertido aproximado

- **Parte 1 — Diagnóstico y optimización SQL Server:** ~65 min
- **Parte 2 — API ASP.NET Core 8 y pruebas (unitarias + integración):** ~60 min
- **Parte 3 — Pantalla de consulta en React:** ~35 min
- **Documentación y verificación final:** ~20 min
- **Total:** ~3 horas
