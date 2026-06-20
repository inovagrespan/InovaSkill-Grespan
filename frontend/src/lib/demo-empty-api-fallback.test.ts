import { describe, expect, it, vi } from "vitest";
import {
  fetchCommercialTransactionsSummary,
  fetchFinanceDashboard,
  fetchProcessingMonitoringDashboard,
  fetchProducts,
} from "@/lib/importer-api";

const authFetchMock = vi.hoisted(() => vi.fn());

vi.mock("@/lib/auth", () => ({
  authFetch: authFetchMock,
}));

describe("demo fallback for empty API responses", () => {
  it("preenche produtos com dados demo de panificação quando a API retorna vazia", async () => {
    authFetchMock.mockResolvedValueOnce(new Response(JSON.stringify({
      Page: 1,
      PageSize: 20,
      Total: 0,
      Items: [],
    }), { status: 200 }));

    const result = await fetchProducts({ page: 1, pageSize: 20 });

    expect(result.total).toBeGreaterThan(0);
    expect(result.items.map((item) => item.name)).toContain("Pão Francês Congelado 60g");
  });

  it("preenche dashboard financeiro com ranking, série e métricas demo quando a API retorna vazia", async () => {
    authFetchMock.mockResolvedValueOnce(new Response(JSON.stringify({
      customers: [],
      summary: { totalRevenue: 0, totalOrders: 0, totalQuantity: 0, averageTicket: 0 },
      revenueTrend: [],
      customerRanking: [],
      items: [],
      page: 1,
      pageSize: 20,
      totalItems: 0,
      totalPages: 1,
    }), { status: 200 }));

    const result = await fetchFinanceDashboard({ page: 1, pageSize: 20, revenueGranularity: "monthly" });

    expect(result.summary.totalRevenue).toBeGreaterThan(0);
    expect(result.revenueTrend.length).toBeGreaterThan(0);
    expect(result.customerRanking.map((item) => item.customer)).toContain("Padaria São Bento");
    expect(result.items.length).toBeGreaterThan(0);
  });

  it("preenche resumo de vendas com dados demo quando a API retorna ranking vazio", async () => {
    authFetchMock.mockResolvedValueOnce(new Response(JSON.stringify({
      page: 1,
      pageSize: 20,
      totalItems: 0,
      granularity: "weekly",
      currentPeriodStart: "2026-06-01",
      previousPeriodStart: "2026-05-01",
      currentPeriodTotalAmount: 0,
      previousPeriodTotalAmount: 0,
      totalGrowthPercent: null,
      totalRecords: 0,
      totalAmount: 0,
      totalQuantity: 0,
      totalWeightKg: 0,
      totalCompanies: 0,
      items: [],
    }), { status: 200 }));

    const result = await fetchCommercialTransactionsSummary({ page: 1, pageSize: 20 });

    expect(result.totalRecords).toBeGreaterThan(0);
    expect(result.items.map((item) => item.companyName)).toContain("Padaria São Bento");
  });

  it("preenche monitoramento de processamentos com jobs demo quando a API retorna vazia", async () => {
    authFetchMock.mockResolvedValueOnce(new Response(JSON.stringify({
      summary: {
        runningJobs: 0,
        queuedJobs: 0,
        completedToday: 0,
        failedJobs: 0,
        averageProcessingSeconds: 0,
        processedRowsToday: 0,
        staleJobs: 0,
      },
      jobs: [],
      daily: [],
      stageDurations: [],
      workers: [],
    }), { status: 200 }));

    const result = await fetchProcessingMonitoringDashboard();

    expect(result.jobs.length).toBeGreaterThan(0);
    expect(result.daily.length).toBeGreaterThan(0);
    expect(result.workers.length).toBeGreaterThan(0);
  });
});
