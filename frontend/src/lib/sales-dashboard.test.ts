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
    granularity: "monthly",
    items: [],
    ...overrides,
  };
}

describe("sales dashboard helpers", () => {
  it("monta o gráfico com uma série temporal real, sem colapsar para anterior x atual", () => {
    const timeline = createTimeline({
      granularity: "monthly",
      items: [
        {
          periodStart: "2026-04-01",
          totalAmount: 1200,
          totalQuantity: 10,
          totalWeightKg: 25,
          recordCount: 2,
        },
        {
          periodStart: "2026-05-01",
          totalAmount: 3200,
          totalQuantity: 20,
          totalWeightKg: 45,
          recordCount: 3,
        },
        {
          periodStart: "2026-06-01",
          totalAmount: 2800,
          totalQuantity: 18,
          totalWeightKg: 41,
          recordCount: 4,
        },
      ],
    });

    expect(buildSalesTrendData(timeline)).toEqual([
      { label: "abr/26", value: 1200, tooltipLabel: "2026-04-01T00:00:00Z" },
      { label: "mai/26", value: 3200, tooltipLabel: "2026-05-01T00:00:00Z" },
      { label: "jun/26", value: 2800, tooltipLabel: "2026-06-01T00:00:00Z" },
    ]);
  });

  it("respeita a granularidade diária e semanal na montagem dos rótulos", () => {
    const dailyTimeline = createTimeline({
      granularity: "daily",
      items: [
        {
          periodStart: "2026-06-08",
          totalAmount: 150,
          totalQuantity: 2,
          totalWeightKg: 8,
          recordCount: 1,
        },
      ],
    });

    const weeklyTimeline = createTimeline({
      granularity: "weekly",
      items: [
        {
          periodStart: "2026-06-08",
          totalAmount: 700,
          totalQuantity: 12,
          totalWeightKg: 28,
          recordCount: 4,
        },
      ],
    });

    expect(buildSalesTrendData(dailyTimeline)).toEqual([
      { label: "08/06", value: 150, tooltipLabel: "2026-06-08T00:00:00Z" },
    ]);
    expect(buildSalesTrendData(weeklyTimeline)).toEqual([
      { label: "Sem 08/06", value: 700, tooltipLabel: "2026-06-08T00:00:00Z" },
    ]);
  });

  it("retorna série vazia quando ainda não há timeline", () => {
    expect(buildSalesTrendData(null)).toEqual([]);
    expect(buildSalesTrendData(createTimeline())).toEqual([]);
  });

  it("gera o texto de comparação correto para cenários positivos, negativos e sem base", () => {
    expect(buildSalesRevenueComparisonText(createSummary())).toBe("Sem resultado para o período e filtros atuais.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: null }))).toBe("Sem base comparativa para o período anterior.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: 12.5 }))).toBe("Faturamento acima do período anterior.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: -3.2 }))).toBe("Faturamento abaixo do período anterior.");
    expect(buildSalesRevenueComparisonText(createSummary({ totalRecords: 4, totalGrowthPercent: 0 }))).toBe("Faturamento igual ao período anterior.");
  });

  it("descreve a granularidade da timeline para a UI", () => {
    expect(describeSalesTimelineGranularity("daily")).toBe("diária");
    expect(describeSalesTimelineGranularity("weekly")).toBe("semanal");
    expect(describeSalesTimelineGranularity("monthly")).toBe("mensal");
  });
});
