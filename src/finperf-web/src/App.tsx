import { useEffect, useState } from "react";
import { getMonthlyPerformance, getPerformanceSummary, getPortfolios } from "./api";
import { MonthlyTable } from "./components/MonthlyTable";
import { SummaryGrid } from "./components/SummaryGrid";
import type {
  MonthlyPerformanceItem,
  PeriodPerformance,
  Portfolio
} from "./types";

export function App() {
  const [portfolios, setPortfolios] = useState<Portfolio[]>([]);
  const [selectedPortfolioId, setSelectedPortfolioId] = useState<number | null>(null);
  const [asOf, setAsOf] = useState<string>("");

  const [summary, setSummary] = useState<PeriodPerformance[] | null>(null);
  const [monthly, setMonthly] = useState<MonthlyPerformanceItem[] | null>(null);

  const [loadingPortfolios, setLoadingPortfolios] = useState<boolean>(true);
  const [loadingData, setLoadingData] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoadingPortfolios(true);
    setError(null);

    getPortfolios(controller.signal)
      .then((list) => {
        setPortfolios(list);
        if (list.length > 0) {
          const first = list[0];
          setSelectedPortfolioId(first.id);
          setAsOf(first.maxDataDate ?? "2023-08-15");
        }
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === "AbortError") {
          return;
        }
        setError(err instanceof Error ? err.message : "No se pudieron cargar los portafolios.");
      })
      .finally(() => {
        setLoadingPortfolios(false);
      });

    return () => controller.abort();
  }, []);

  const currentPortfolio = portfolios.find((p) => p.id === selectedPortfolioId) ?? null;

  useEffect(() => {
    if (!currentPortfolio || !asOf) {
      return;
    }

    const controller = new AbortController();
    setLoadingData(true);
    setError(null);

    const monthlyFrom = currentPortfolio.minDataDate ?? currentPortfolio.inceptionDate;

    Promise.all([
      getPerformanceSummary(currentPortfolio.id, asOf, controller.signal),
      getMonthlyPerformance(currentPortfolio.id, monthlyFrom, asOf, controller.signal)
    ])
      .then(([summaryRes, monthlyRes]) => {
        setSummary(summaryRes.periods);
        setMonthly(monthlyRes.months);
      })
      .catch((err: unknown) => {
        if (err instanceof DOMException && err.name === "AbortError") {
          return;
        }
        setSummary(null);
        setMonthly(null);
        setError(err instanceof Error ? err.message : "Error al consultar el rendimiento.");
      })
      .finally(() => {
        setLoadingData(false);
      });

    return () => controller.abort();
  }, [currentPortfolio, asOf]);

  return (
    <main className="container">
      <header className="page-header">
        <h1>Rendimiento de Portafolios</h1>
      </header>

      <section className="filters">
        <div className="field">
          <label htmlFor="portfolio-select">Portafolio</label>
          <select
            id="portfolio-select"
            value={selectedPortfolioId ?? ""}
            disabled={loadingPortfolios || portfolios.length === 0}
            onChange={(e) => setSelectedPortfolioId(Number(e.target.value))}
          >
            {portfolios.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} ({p.baseCurrency})
              </option>
            ))}
          </select>
        </div>

        <div className="field">
          <label htmlFor="asof-input">Fecha de corte</label>
          <input
            id="asof-input"
            type="date"
            value={asOf}
            min={currentPortfolio?.minDataDate ?? undefined}
            max={currentPortfolio?.maxDataDate ?? undefined}
            disabled={loadingPortfolios || !currentPortfolio}
            onChange={(e) => setAsOf(e.target.value)}
          />
        </div>
      </section>

      {loadingPortfolios && <p className="state-box">Cargando portafolios...</p>}

      {!loadingPortfolios && portfolios.length === 0 && !error && (
        <p className="state-box">No hay portafolios disponibles.</p>
      )}

      {error && (
        <div className="error-box" role="alert">
          {error}
        </div>
      )}

      {loadingData && <p className="state-box">Consultando rendimiento...</p>}

      {!loadingData && !error && summary && (
        <>
          <SummaryGrid periods={summary} />
          <section className="monthly-section">
            <h2>Retornos mensuales</h2>
            {monthly ? (
              <MonthlyTable items={monthly} />
            ) : (
              <p className="state-box">Sin datos mensuales disponibles.</p>
            )}
          </section>
        </>
      )}
    </main>
  );
}
