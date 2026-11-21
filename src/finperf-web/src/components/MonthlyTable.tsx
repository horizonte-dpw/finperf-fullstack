import { formatCurrency, formatPercent, MONTH_LABELS } from "../formatters";
import type { MonthlyPerformanceItem } from "../types";

interface MonthlyTableProps {
  items: MonthlyPerformanceItem[];
}

export function MonthlyTable({ items }: MonthlyTableProps) {
  if (items.length === 0) {
    return <p className="state-box">No hay datos mensuales para el período seleccionado.</p>;
  }

  const byYear = new Map<number, Map<number, MonthlyPerformanceItem>>();
  for (const item of items) {
    let months = byYear.get(item.year);
    if (!months) {
      months = new Map<number, MonthlyPerformanceItem>();
      byYear.set(item.year, months);
    }
    months.set(item.month, item);
  }

  const years = Array.from(byYear.keys()).sort((a, b) => b - a);

  return (
    <div className="table-wrapper">
      <table className="monthly-table">
        <thead>
          <tr>
            <th>Año</th>
            {MONTH_LABELS.map((label) => (
              <th key={label}>{label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {years.map((year) => {
            const months = byYear.get(year)!;
            return (
              <tr key={year}>
                <td className="year-col">{year}</td>
                {MONTH_LABELS.map((_, idx) => {
                  const monthNumber = idx + 1;
                  const cell = months.get(monthNumber);
                  if (!cell) {
                    return (
                      <td key={monthNumber} className="empty-cell">
                        —
                      </td>
                    );
                  }
                  const cls = cell.cumulativeReturn >= 0 ? "positive" : "negative";
                  return (
                    <td
                      key={monthNumber}
                      className={cls}
                      title={`P&L: ${formatCurrency(cell.pnL)} (${cell.daysCount} días)`}
                    >
                      {formatPercent(cell.cumulativeReturn)}
                    </td>
                  );
                })}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
