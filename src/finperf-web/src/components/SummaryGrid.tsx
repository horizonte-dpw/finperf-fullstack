import { formatCurrency, formatPercent } from "../formatters";
import type { PeriodPerformance } from "../types";

interface SummaryGridProps {
  periods: PeriodPerformance[];
}

export function SummaryGrid({ periods }: SummaryGridProps) {
  return (
    <section className="summary-grid" aria-label="Resumen de rendimiento">
      {periods.map((item) => {
        const valueClass =
          item.cumulativeReturn === null
            ? "neutral"
            : item.cumulativeReturn >= 0
              ? "positive"
              : "negative";

        return (
          <article key={item.period} className="summary-card">
            <div className="summary-header">
              <span className="period-badge">{item.period}</span>
              <span className="period-range">
                {item.from} – {item.to}
              </span>
            </div>
            <div className={`summary-return ${valueClass}`}>
              {formatPercent(item.cumulativeReturn)}
            </div>
            <div className="summary-pnl">
              P&amp;L: <strong>{formatCurrency(item.pnL)}</strong>
            </div>
            <div className="summary-days">{item.daysCount} días hábiles</div>
          </article>
        );
      })}
    </section>
  );
}
