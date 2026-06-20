import { describe, expect, it, vi } from "vitest";
import {
  fetchCommercialTransactions,
  fetchCommercialTransactionsSummary,
  fetchCommercialTransactionsTimeline,
} from "@/lib/importer-api";

vi.mock("@/lib/auth", () => ({
  authFetch: vi.fn(() => {
    throw new Error("Failed to fetch");
  }),
}));

describe("sales demo fallback", () => {
  it("reflete a data atual da base demo no filtro Hoje de vendas", async () => {
    const response = await fetchCommercialTransactions({
      dateFrom: "2026-06-08",
      dateTo: "2026-06-08",
    });

    expect(response.total).toBe(1);
    expect(response.items).toHaveLength(1);
    expect(response.items[0]).toMatchObject({
      documentNumber: "DEV-2026-001",
      transactionDate: "2026-06-08",
      customerName: "Supermercado Primavera",
      transactionType: "Devolução",
    });
  });

  it("mantem resumo, totais e ranking consistentes com filtros e valores negativos", async () => {
    const summary = await fetchCommercialTransactionsSummary({
      dateFrom: "2026-06-08",
      dateTo: "2026-06-08",
      sortBy: "amount",
    });

    expect(summary.totalRecords).toBe(1);
    expect(summary.totalAmount).toBe(-669.6);
    expect(summary.currentPeriodTotalAmount).toBe(summary.totalAmount);
    expect(summary.totalQuantity).toBe(-24);
    expect(summary.totalWeightKg).toBe(-120);
    expect(summary.totalCompanies).toBe(1);
    expect(summary.items).toEqual([
      {
        companyName: "Supermercado Primavera",
        documentCount: 1,
        singleDocumentNumber: "DEV-2026-001",
        totalAmount: -669.6,
        totalQuantity: -24,
        totalWeightKg: -120,
        currentPeriodAmount: -669.6,
        previousPeriodAmount: 0,
        growthPercent: null,
      },
    ]);
  });

  it("gera timeline diaria filtrada pela mesma data usada na tela", async () => {
    const timeline = await fetchCommercialTransactionsTimeline({
      granularity: "day",
      dateFrom: "2026-06-08",
      dateTo: "2026-06-08",
    });

    expect(timeline).toEqual({
      granularity: "day",
      items: [
        {
          periodStart: "2026-06-08",
          totalAmount: -669.6,
          totalQuantity: -24,
          totalWeightKg: -120,
          recordCount: 1,
        },
      ],
    });
  });
});
