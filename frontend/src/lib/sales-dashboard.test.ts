import { describe, expect, it } from "vitest";
import type {
  CommercialTransactionSummaryResponse,
  CommercialTransactionTimelineResponse,
} from "@/lib/importer-api";
import {
  buildSalesRevenueComparisonText,
  buildSalesTrendData,
  describeSalesTimelineGranularity,
} from "./sales-dashboard";

function createSummary(
  overrides: Partial<CommercialTransactionSummaryResponse> = {},
): CommercialTransactionSummaryResponse {
  return {
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
    ...overrides,
  };
}

function createTimeline(
  overrides: Partial<CommercialTransactionTimelineResponse> = {},
): CommercialTransactionTimelineResponse {
  return {
    granularity: "month",
    items: [],
    ...overrides,
  };
}

describe("sales dashboard helpers", () => {
  it("monta o grafico com uma serie temporal real, sem colapsar para anterior x atual", () => {
    const timeline = createTimeline({
      granularity: "month",
      items: [
        { periodStart: "2026-04-01", totalAmount: 1200, totalQuantity: 10, totalWeightKg: 25, recordCount: 2 },
        { periodStart: "2026-05-01", totalAmount: 3200, totalQuantity: 20, totalWeightKg: 45, recordCount: 3 },
        { periodStart: "2026-06-01", totalAmount: 2800, totalQuantity: 18, totalWeightKg: 41, recordCount: 4 },
      ],
    });

    expect(buildSalesTrendData(timeline)).toEqual([
      { label: "Abr", value: 1200, tooltipLabel: "2026-04-01T00:00:00Z" },
      { label: "Mai", value: 3200, tooltipLabel: "2026-05-01T00:00:00Z" },
      { label: "Jun", value: 2800, tooltipLabel: "2026-06-01T00:00:00Z" },
    ]);
  });

  it("respeita granularidades de hora, dia e semana na montagem dos rotulos", () => {
    const hourTimeline = createTimeline({
      granularity: "hour",
      items: [{ periodStart: "2026-06-08T08:00:00Z", totalAmount: 150, totalQuantity: 2, totalWeightKg: 8, recordCount: 1 }],
    });
    const dailyTimeline = createTimeline({
      granularity: "day",
      items: [{ periodStart: "2026-06-08", totalAmount: 150, totalQuantity: 2, totalWeightKg: 8, recordCount: 1 }],
    });
    const weeklyTimeline = createTimeline({
      granularity: "week",
      items: [{ periodStart: "2026-06-08", totalAmount: 700, totalQuantity: 12, totalWeightKg: 28, recordCount: 4 }],
    });

    expect(buildSalesTrendData(hourTimeline)).toEqual([
      { label: "08h", value: 150, tooltipLabel: "2026-06-08T08:00:00Z" },
    ]);
    expect(buildSalesTrendData(dailyTimeline)).toEqual([
      { label: "08 de jun", value: 150, tooltipLabel: "2026-06-08T00:00:00Z" },
    ]);
    expect(buildSalesTrendData(weeklyTimeline)).toEqual([
      { label: "08 de jun - 14 de jun", value: 700, tooltipLabel: "2026-06-08T00:00:00Z" },
    ]);
  });

  it("retorna serie vazia quando ainda nao ha timeline", () => {
    expect(buildSalesTrendData(null)).toEqual([]);
    expect(buildSalesTrendData(createTimeline())).toEqual([]);
  });

  it("gera o texto de comparacao correto para cenarios positivos, negativos e sem base", () => {
    expect(buildSalesRevenueComparisonText(createSummary())).toBe("Sem resultado para o período e filtros atuais.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: null }))).toBe("Sem base comparativa para o período anterior.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: 12.5 }))).toBe("Faturamento acima do período anterior.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: -3.2 }))).toBe("Faturamento abaixo do período anterior.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: 0 }))).toBe("Faturamento igual ao período anterior.");
  });

  it("descreve a granularidade da timeline para a UI", () => {
    expect(describeSalesTimelineGranularity("hour")).toBe("hora");
    expect(describeSalesTimelineGranularity("day")).toBe("dia");
    expect(describeSalesTimelineGranularity("week")).toBe("semana");
    expect(describeSalesTimelineGranularity("month")).toBe("mês");
    expect(describeSalesTimelineGranularity("quarter")).toBe("trimestre");
  });
});
