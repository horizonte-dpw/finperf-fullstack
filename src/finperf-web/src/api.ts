import type {
  MonthlyPerformanceResponse,
  PerformanceSummaryResponse,
  Portfolio,
  ProblemDetails
} from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080";

async function fetchJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE_URL}${path}`, { signal });
  if (!response.ok) {
    let message = `Error HTTP ${response.status}`;
    try {
      const problem = (await response.json()) as ProblemDetails;
      if (problem.detail) {
        message = problem.detail;
      } else if (problem.title) {
        message = problem.title;
      }
    } catch {
    }
    throw new Error(message);
  }

  return (await response.json()) as T;
}

export function getPortfolios(signal?: AbortSignal): Promise<Portfolio[]> {
  return fetchJson<Portfolio[]>("/api/portfolios", signal);
}

export function getPerformanceSummary(
  portfolioId: number,
  asOf: string,
  signal?: AbortSignal
): Promise<PerformanceSummaryResponse> {
  const params = new URLSearchParams({ asOf });
  return fetchJson<PerformanceSummaryResponse>(
    `/api/portfolios/${portfolioId}/performance/summary?${params.toString()}`,
    signal
  );
}

export function getMonthlyPerformance(
  portfolioId: number,
  from: string,
  to: string,
  signal?: AbortSignal
): Promise<MonthlyPerformanceResponse> {
  const params = new URLSearchParams({ from, to });
  return fetchJson<MonthlyPerformanceResponse>(
    `/api/portfolios/${portfolioId}/performance/monthly?${params.toString()}`,
    signal
  );
}
