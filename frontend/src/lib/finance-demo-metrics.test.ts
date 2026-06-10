import { describe, expect, it } from "vitest";
import {
  buildFinanceCustomerRevenueRanking,
  buildFinanceMonthlyRevenue,
  buildFinanceRevenueTrend,
  calculateFinanceMetrics,
  listFinanceCustomers,
  type FinanceDemoTransaction,
} from "./finance-demo-metrics";

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

  it("filtra cliente por trecho sem exigir acento exato no fallback", () => {
    const result = calculateFinanceMetrics(
      { customer: "sao", dateFrom: "2026-01-01", dateTo: "2026-12-31", allTime: false },
      [
        { customer: "Padaria São Bento", date: "2026-01-10", revenue: 1_000, orders: 1, quantity: 10 },
        { customer: "Supermercado Primavera", date: "2026-01-10", revenue: 2_000, orders: 1, quantity: 20 },
      ],
    );

    expect(result.totalRevenue).toBe(1_000);
    expect(result.items).toEqual([
      { customer: "Padaria São Bento", date: "2026-01-10", revenue: 1_000, orders: 1, quantity: 10 },
    ]);
  });

  it("soma faturamento, ticket medio e quantidade do periodo filtrado", () => {
    const result = calculateFinanceMetrics(
      { customer: "", dateFrom: "2026-01-01", dateTo: "2026-02-28", allTime: false },
      sample,
    );

    expect(result.totalRevenue).toBe(7_000);
    expect(result.totalOrders).toBe(8);
    expect(result.totalQuantity).toBe(70);
    expect(result.averageTicket).toBe(875);
  });

  it("ignora datas quando tempo total esta ativo", () => {
    const result = calculateFinanceMetrics(
      { customer: "", dateFrom: "2026-03-01", dateTo: "2026-03-31", allTime: true },
      sample,
    );

    expect(result.totalRevenue).toBe(7_000);
    expect(result.items).toHaveLength(3);
  });

  it("lista clientes disponiveis para o filtro", () => {
    expect(listFinanceCustomers(sample)).toEqual(["Cliente A", "Cliente B"]);
  });

  it("retorna metricas zeradas quando nenhum cliente entra no filtro", () => {
    const result = calculateFinanceMetrics(
      { customer: "Cliente C", dateFrom: "2026-01-01", dateTo: "2026-12-31", allTime: false },
      sample,
    );

    expect(result.totalRevenue).toBe(0);
    expect(result.totalOrders).toBe(0);
    expect(result.totalQuantity).toBe(0);
    expect(result.averageTicket).toBe(0);
    expect(result.items).toEqual([]);
  });

  it("agrega faturamento mensal em ordem cronologica para graficos", () => {
    const result = buildFinanceMonthlyRevenue(sample);

    expect(result).toEqual([
      { period: "2026-01", label: "jan", revenue: 1_000 },
      { period: "2026-02", label: "fev", revenue: 6_000 },
    ]);
  });

  it("agrega faturamento semanal e anual para busca do grafico financeiro", () => {
    expect(buildFinanceRevenueTrend(sample, "weekly")).toEqual([
      { period: "2026-01-05", label: "Sem 05/01", revenue: 1_000 },
      { period: "2026-02-09", label: "Sem 09/02", revenue: 6_000 },
    ]);

    expect(buildFinanceRevenueTrend(sample, "yearly")).toEqual([
      { period: "2026", label: "2026", revenue: 7_000 },
    ]);
  });

  it("monta ranking de faturamento por cliente com desempate alfabetico", () => {
    const result = buildFinanceCustomerRevenueRanking([
      ...sample,
      { customer: "Cliente C", date: "2026-03-10", revenue: 3_000, orders: 1, quantity: 5 },
    ]);

    expect(result).toEqual([
      { customer: "Cliente B", revenue: 4_000 },
      { customer: "Cliente A", revenue: 3_000 },
      { customer: "Cliente C", revenue: 3_000 },
    ]);
  });
});
