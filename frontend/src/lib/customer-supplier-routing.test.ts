import { describe, expect, it } from "vitest";
import { buildSupplierRouteDashboard } from "./customer-supplier-routing";
import { getDemoCustomerFinanceImpact } from "./customer-finance-impact-demo";

const REFERENCE_DATE = new Date("2026-06-18T13:00:00.000Z");

describe("customer supplier routing", () => {
  it("consome fornecedor e rota vindos da integracao, sem cadastro manual", () => {
    const dashboard = buildSupplierRouteDashboard(getDemoCustomerFinanceImpact().risco, "todos", REFERENCE_DATE);

    expect(dashboard.summary.riskCustomers).toBe(4);
    expect(dashboard.suppliers.map((supplier) => supplier.supplierName)).toEqual([
      "Bruno Almeida",
      "Camila Rocha",
      "Marina Costa",
      "Rafael Mendes",
    ]);
    expect(dashboard.routes.flatMap((route) => route.customers)).toEqual(expect.arrayContaining([
      expect.objectContaining({
        customerId: "CLI-002",
        supplierId: "VEN-018",
        supplierName: "Rafael Mendes",
        routeName: "Rota Ribeirão Preto",
      }),
      expect.objectContaining({
        customerId: "CLI-004",
        supplierId: "VEN-024",
        supplierName: "Bruno Almeida",
        routeName: "Rota Sorocaba",
      }),
      expect.objectContaining({
        customerId: "CLI-005",
        supplierId: "VEN-031",
        supplierName: "Marina Costa",
        routeName: "Rota Campinas",
      }),
      expect.objectContaining({
        customerId: "CLI-006",
        supplierId: "VEN-027",
        supplierName: "Camila Rocha",
        routeName: "Rota São Paulo Oeste",
      }),
    ]));
  });

  it("filtra indicadores e clientes pelo fornecedor selecionado", () => {
    const dashboard = buildSupplierRouteDashboard(getDemoCustomerFinanceImpact().risco, "VEN-018", REFERENCE_DATE);

    expect(dashboard.summary.totalCustomers).toBe(1);
    expect(dashboard.summary.riskCustomers).toBe(1);
    expect(dashboard.routes).toHaveLength(1);
    expect(dashboard.routes[0].customers[0]).toEqual(expect.objectContaining({
      supplierName: "Rafael Mendes",
      customerName: "Supermercado Primavera",
    }));
  });

  it("classifica risco em atencao, alto e critico com cores padronizaveis na tela", () => {
    const dashboard = buildSupplierRouteDashboard([
      { clienteId: "CLI-A", clienteNome: "Cliente Atenção", fornecedorNome: "Ana", rotaNome: "Rota A", nivelRisco: "Atenção", scoreRisco: 45 },
      { clienteId: "CLI-H", clienteNome: "Cliente Alto", fornecedorNome: "Bia", rotaNome: "Rota B", nivelRisco: "Alto", scoreRisco: 70, horasDesdeNotificacao: 2 },
      { clienteId: "CLI-C", clienteNome: "Cliente Crítico", fornecedorNome: "Caio", rotaNome: "Rota C", nivelRisco: "Crítico", scoreRisco: 90, horasDesdeNotificacao: 2 },
    ], "todos", REFERENCE_DATE);

    expect(dashboard.riskBuckets).toEqual({ attention: 1, high: 1, critical: 1 });
    expect(dashboard.summary.notifiedCustomers).toBe(2);
    expect(dashboard.summary.criticalCustomers).toBe(1);
  });

  it("notifica alto e critico, registra horario e escala para gerencia sem acao no prazo", () => {
    const dashboard = buildSupplierRouteDashboard(getDemoCustomerFinanceImpact().risco, "todos", REFERENCE_DATE);
    const situations = dashboard.routes.flatMap((route) => route.customers);

    expect(situations.find((situation) => situation.customerId === "CLI-002")).toEqual(expect.objectContaining({
      elapsedHours: 52,
      remainingHours: 0,
      responseDeadlineHours: 24,
      notificationSentAt: "2026-06-16T09:00:00.000Z",
      status: "escalated",
    }));
    expect(dashboard.managementQueue).toEqual(expect.arrayContaining([
      expect.objectContaining({ customerId: "CLI-002", status: "escalated" }),
      expect.objectContaining({ customerId: "CLI-005", status: "escalated" }),
    ]));
  });

  it("mantem fornecedor nao informado apenas como dado ausente da integracao", () => {
    const dashboard = buildSupplierRouteDashboard([
      {
        clienteId: "CLI-999",
        clienteNome: "Cliente sem fornecedor na base",
        nivelRisco: "Alto",
        mesesQueda: "1 mês",
        impactoFinanceiro: 1000,
        horasDesdeNotificacao: 1,
      },
    ], "todos", REFERENCE_DATE);

    expect(dashboard.routes[0].customers[0]).toEqual(expect.objectContaining({
      customerId: "CLI-999",
      supplierName: "Fornecedor não informado",
      routeName: "Rota não informada",
      status: "notified",
    }));
  });
});
