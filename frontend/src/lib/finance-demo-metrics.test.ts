import { describe, expect, it } from "vitest";
import { calculateFinanceMetrics, listFinanceCustomers, type FinanceDemoTransaction } from "./finance-demo-metrics";

const sample: FinanceDemoTransaction[] = [
  { customer: "Cliente A", date: "2026-01-10", revenue: 1_000, orders: 2, quantity: 10 },
  { customer: "Cliente A", date: "2026-02-10", revenue: 2_000, orders: 2, quantity: 20 },
  { customer: "Cliente B", date: "2026-02-10", revenue: 4_000, orders: 4, quantity: 40 },
];

describe("finance demo metrics", () => {
  it("filtra por cliente e intervalo de datas", () => {
    const result = calculateFinanceMetrics(
      { customer: "Cliente A", dateFrom: "2026-02-01", dateTo: "2026-02-28", allTime: false },
      sample,
    );

    expect(result.totalRevenue).toBe(2_000);
    expect(result.totalOrders).toBe(2);
    expect(result.totalQuantity).toBe(20);
    expect(result.averageTicket).toBe(1_000);
  });

  it("ignora datas quando tempo total está ativo", () => {
    const result = calculateFinanceMetrics(
      { customer: "", dateFrom: "2026-03-01", dateTo: "2026-03-31", allTime: true },
      sample,
    );

    expect(result.totalRevenue).toBe(7_000);
    expect(result.items).toHaveLength(3);
  });

  it("lista clientes disponíveis para o filtro", () => {
    expect(listFinanceCustomers(sample)).toEqual(["Cliente A", "Cliente B"]);
  });
});
