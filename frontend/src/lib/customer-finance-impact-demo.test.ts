import { describe, expect, it } from "vitest";

import { getDemoCustomerFinanceImpact, hasCustomerFinanceImpactData } from "./customer-finance-impact-demo";

describe("customer finance impact demo", () => {
  it("fornece dados fictícios para resumo, alertas, risco, crescimento e oportunidades", () => {
    const result = getDemoCustomerFinanceImpact();

    expect(result.resumo.maiorCliente).toBe("Padaria São Bento");
    expect(result.resumo.maiorFaturamento).toBeGreaterThan(0);
    expect(result.alertas.length).toBeGreaterThan(0);
    expect(result.risco.map((item) => item.clienteNome)).toContain("Supermercado Primavera");
    expect(result.crescimento.map((item) => item.clienteNome)).toContain("Padaria São Bento");
    expect(result.oportunidades[0]).toEqual(expect.objectContaining({
      scorePotencial: expect.any(Number),
      ticketMedioGeral: expect.any(Number),
      frequenciaCompra: expect.any(Number),
    }));
  });

  it("identifica resposta vazia da API para acionar fallback demo", () => {
    expect(hasCustomerFinanceImpactData({
      risco: [],
      crescimento: [],
      alertas: [],
      oportunidades: [],
      resumo: {},
    })).toBe(false);

    expect(hasCustomerFinanceImpactData(getDemoCustomerFinanceImpact())).toBe(true);
  });
});
