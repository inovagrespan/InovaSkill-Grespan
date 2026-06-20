import { describe, expect, it } from "vitest";
import type { CommercialInvoiceAnalyticsResponse } from "@/lib/importer-api";
import {
  buildSalesRankingData,
  buildSalesTrendData,
  describeSalesRankingMetric,
  describeSalesTimelineGranularity,
  describeSalesTrendMetric,
} from "./sales-dashboard";

function createAnalytics(
  overrides: Partial<CommercialInvoiceAnalyticsResponse> = {},
): CommercialInvoiceAnalyticsResponse {
  return {
    granularity: "month",
    summary: {
      totalInvoices: 4,
      totalAmount: 6200,
      totalWeightKg: 140,
      totalCustomers: 3,
      totalItems: 11,
      totalQuantity: 28,
    },
    trend: [
      { periodStart: "2026-04-01", invoiceCount: 2, totalAmount: 1200, totalWeightKg: 25 },
      { periodStart: "2026-05-01", invoiceCount: 1, totalAmount: 3200, totalWeightKg: 45 },
      { periodStart: "2026-06-01", invoiceCount: 1, totalAmount: 1800, totalWeightKg: 70 },
    ],
    ranking: [
      { customerCode: "C2", customerName: "Cliente B", totalAmount: 2800, invoiceCount: 1, totalItems: 2, totalWeightKg: 18 },
      { customerCode: "C1", customerName: "Cliente A", totalAmount: 2200, invoiceCount: 3, totalItems: 7, totalWeightKg: 42 },
      { customerCode: "C3", customerName: "Cliente C", totalAmount: 900, invoiceCount: 2, totalItems: 9, totalWeightKg: 65 },
    ],
    ...overrides,
  };
}

describe("sales dashboard helpers", () => {
  it("monta a evolução de notas pela métrica selecionada", () => {
    const analytics = createAnalytics();

    expect(buildSalesTrendData(analytics, "invoiceCount")).toEqual([
      { label: "Abr", value: 2, tooltipLabel: "2026-04-01T00:00:00Z" },
      { label: "Mai", value: 1, tooltipLabel: "2026-05-01T00:00:00Z" },
      { label: "Jun", value: 1, tooltipLabel: "2026-06-01T00:00:00Z" },
    ]);

    expect(buildSalesTrendData(analytics, "totalAmount")[1]?.value).toBe(3200);
    expect(buildSalesTrendData(analytics, "totalWeightKg")[2]?.value).toBe(70);
  });

  it("retorna série vazia quando ainda não há análise", () => {
    expect(buildSalesTrendData(null, "invoiceCount")).toEqual([]);
    expect(buildSalesRankingData(null, "amount")).toEqual([]);
  });

  it("ordena o ranking por valor, notas, itens e peso", () => {
    const analytics = createAnalytics();

    expect(buildSalesRankingData(analytics, "amount").map((item) => item.companyName)).toEqual([
      "Cliente B",
      "Cliente A",
      "Cliente C",
    ]);
    expect(buildSalesRankingData(analytics, "invoiceCount").map((item) => item.companyName)).toEqual([
      "Cliente A",
      "Cliente C",
      "Cliente B",
    ]);
    expect(buildSalesRankingData(analytics, "items").map((item) => item.companyName)).toEqual([
      "Cliente C",
      "Cliente A",
      "Cliente B",
    ]);
    expect(buildSalesRankingData(analytics, "weight").map((item) => item.companyName)).toEqual([
      "Cliente C",
      "Cliente A",
      "Cliente B",
    ]);
  });

  it("descreve granularidade e rótulos das métricas para a UI", () => {
    expect(describeSalesTimelineGranularity("day")).toBe("dia");
    expect(describeSalesTimelineGranularity("week")).toBe("semana");
    expect(describeSalesTimelineGranularity("month")).toBe("mês");
    expect(describeSalesTrendMetric("invoiceCount")).toBe("Quantidade de notas emitidas");
    expect(describeSalesTrendMetric("totalAmount")).toBe("Valor total das notas");
    expect(describeSalesTrendMetric("totalWeightKg")).toBe("Peso total movimentado");
    expect(describeSalesRankingMetric("amount")).toBe("Valor total das notas por cliente");
    expect(describeSalesRankingMetric("invoiceCount")).toBe("Quantidade de notas por cliente");
    expect(describeSalesRankingMetric("items")).toBe("Quantidade de itens por cliente");
    expect(describeSalesRankingMetric("weight")).toBe("Peso total movimentado por cliente");
  });
});
