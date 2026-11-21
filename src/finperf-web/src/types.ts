export interface Portfolio {
  id: number;
  name: string;
  inceptionDate: string;
  baseCurrency: string;
  minDataDate: string | null;
  maxDataDate: string | null;
}

export interface PeriodPerformance {
  period: "MTD" | "QTD" | "YTD" | "ITD" | string;
  from: string;
  to: string;
  cumulativeReturn: number | null;
  pnL: number;
  daysCount: number;
}

export interface PerformanceSummaryResponse {
  portfolioId: number;
  asOf: string;
  periods: PeriodPerformance[];
}

export interface MonthlyPerformanceItem {
  year: number;
  month: number;
  cumulativeReturn: number;
  pnL: number;
  daysCount: number;
}

export interface MonthlyPerformanceResponse {
  portfolioId: number;
  from: string;
  to: string;
  months: MonthlyPerformanceItem[];
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
}
