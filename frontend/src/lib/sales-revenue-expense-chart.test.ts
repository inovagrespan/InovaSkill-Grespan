import { describe, expect, it } from "vitest";
import { buildSalesRevenueExpenseSeries, DEMO_SALES_FINANCIAL_RECORDS, SALES_FINANCIAL_REFERENCE_DATE, type SalesFinancialRecord } from "./sales-revenue-expense-chart";

const records: SalesFinancialRecord[] = [
  { date: "2025-12-31", revenue: 100, expenses: 40 },
  { date: "2026-06-23", revenue: 200.125, expenses: 80.126 },
  { date: "2026-06-23", revenue: 50.126, expenses: 20.125 },
  { date: "2026-06-24", revenue: 300, expenses: 120 },
  { date: "2026-06-24", revenue: null, expenses: null },
  { date: "2024-03-10", revenue: 500, expenses: 250 },
  { date: "inválida", revenue: 999, expenses: 999 },
  { date: "2026-06-22", revenue: -10, expenses: Number.NaN },
];

describe("sales revenue and expense chart", () => {
  it("agrega quatorze dias incluindo o limite inicial e final", () => {
    const series = buildSalesRevenueExpenseSeries(records, "daily", "2026-06-24");

    expect(series).toHaveLength(14);
    expect(series[0].period).toBe("2026-06-11");
    expect(series.at(-1)).toEqual(expect.objectContaining({ period: "2026-06-24", revenue: 300, expenses: 120 }));
    expect(series.find((point) => point.period === "2026-06-23")).toEqual(expect.objectContaining({ revenue: 250.26, expenses: 100.26 }));
  });

  it("agrupa por mês, preenche ausência com zero e mantém soma das partes", () => {
    const series = buildSalesRevenueExpenseSeries(records, "monthly", "2026-06-24");

    expect(series).toHaveLength(12);
    expect(series.map((point) => point.period)).toEqual([
      "2025-07", "2025-08", "2025-09", "2025-10", "2025-11", "2025-12",
      "2026-01", "2026-02", "2026-03", "2026-04", "2026-05", "2026-06",
    ]);
    expect(series[1]).toEqual(expect.objectContaining({ revenue: 0, expenses: 0 }));
    expect(series.reduce((total, point) => total + point.revenue, 0)).toBe(650.26);
    expect(series.reduce((total, point) => total + point.expenses, 0)).toBe(260.26);
  });

  it("agrupa seis anos, ignora datas inválidas e mantém valores não negativos", () => {
    const series = buildSalesRevenueExpenseSeries(records, "yearly", "2026-06-24");

    expect(series.map((point) => point.period)).toEqual(["2021", "2022", "2023", "2024", "2025", "2026"]);
    expect(series.find((point) => point.period === "2024")).toEqual(expect.objectContaining({ revenue: 500, expenses: 250 }));
    expect(series.every((point) => point.revenue >= 0 && point.expenses >= 0)).toBe(true);
    expect(series.reduce((total, point) => total + point.revenue, 0)).toBe(1_150.26);
  });

  it("retorna série vazia quando a data de referência é inválida", () => {
    expect(buildSalesRevenueExpenseSeries(records, "daily", "inválida")).toEqual([]);
  });

  it("mantém oscilações visíveis de alta e queda nos períodos demonstrativos", () => {
    for (const period of ["daily", "monthly", "yearly"] as const) {
      const series = buildSalesRevenueExpenseSeries(DEMO_SALES_FINANCIAL_RECORDS, period, SALES_FINANCIAL_REFERENCE_DATE);
      const revenueChanges = series.slice(1).map((point, index) => Math.sign(point.revenue - series[index].revenue));
      const expenseChanges = series.slice(1).map((point, index) => Math.sign(point.expenses - series[index].expenses));

      expect(revenueChanges).toContain(1);
      expect(revenueChanges).toContain(-1);
      expect(expenseChanges).toContain(1);
      expect(expenseChanges).toContain(-1);
    }
  });

  it("mantém poucos períodos demonstrativos com gastos acima do faturamento", () => {
    for (const period of ["daily", "monthly", "yearly"] as const) {
      const series = buildSalesRevenueExpenseSeries(DEMO_SALES_FINANCIAL_RECORDS, period, SALES_FINANCIAL_REFERENCE_DATE);
      const overspendCount = series.filter((point) => point.expenses > point.revenue).length;

      expect(overspendCount).toBeGreaterThanOrEqual(1);
      expect(overspendCount).toBeLessThanOrEqual(2);
    }
  });
});
