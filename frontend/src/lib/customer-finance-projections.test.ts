import { describe, expect, it, vi } from "vitest";

import {
  calculateProjectedMarginPercent,
  calculateSimulatedProjectedCost,
  enrichCustomerFinanceProjectionCosts,
  fetchCustomerFinanceProjections,
  getDemoCustomerFinanceProjections,
  hasCustomerFinanceProjectionData,
} from "./customer-finance-projections";

const authFetchMock = vi.hoisted(() => vi.fn());

vi.mock("@/lib/auth", () => ({
  authFetch: authFetchMock,
}));

describe("customer finance projections", () => {
  it("fornece dados demo para KPIs, tendências e tabela de evolução", () => {
    const demo = getDemoCustomerFinanceProjections();

    expect(demo.projecoes.proximoMes).toBeGreaterThan(0);
    expect(demo.tendencias.length).toBeGreaterThan(0);
    expect(demo.evolucaoClientes.length).toBeGreaterThan(0);
    expect(demo.evolucaoClientes[0]).toEqual(expect.objectContaining({
      clienteNome: expect.any(String),
      valorAtual: expect.any(Number),
      valorProjetado: expect.any(Number),
      custoProjetado: expect.any(Number),
      margemProjetadaPercentual: expect.any(Number),
      tendenciaPrevista: expect.any(String),
      confiancaModelo: expect.any(Number),
    }));
  });

  it("simula custo e margem projetada com taxa deterministica por linha", () => {
    expect(calculateSimulatedProjectedCost(10_000, 0)).toBe(6_100);
    expect(calculateSimulatedProjectedCost(10_000, 1)).toBe(6_400);
    expect(calculateSimulatedProjectedCost(10_000, 5)).toBe(6_100);
    expect(calculateProjectedMarginPercent(10_000, 6_100)).toBe(39);
  });

  it("mantem custo e margem zerados quando nao existe receita projetada", () => {
    expect(calculateSimulatedProjectedCost(0, 0)).toBe(0);
    expect(calculateProjectedMarginPercent(0, 1_000)).toBe(0);
  });

  it("enriquece a tabela de evolucao com custos simulados sem sobrescrever valores reais", () => {
    const result = enrichCustomerFinanceProjectionCosts({
      projecoes: {
        faturamentoMensalAtual: 10_000,
        proximoMes: 11_000,
        proximos3Meses: 33_000,
        proximos6Meses: 66_000,
        proximos12Meses: 132_000,
      },
      cenarioOtimista: { margem: 15, descricao: "Otimista (+15%)" },
      cenarioRealista: { margem: 0, descricao: "Realista" },
      cenarioConservador: { margem: -15, descricao: "Conservador (-15%)" },
      tendencias: [],
      evolucaoClientes: [
        {
          clienteId: "CLI-001",
          clienteNome: "Cliente A",
          valorAtual: 9_000,
          valorProjetado: 10_000,
          diferenca: 1_000,
          tendenciaPrevista: "Crescimento",
          confiancaModelo: 0.9,
        },
        {
          clienteId: "CLI-002",
          clienteNome: "Cliente B",
          valorAtual: 8_000,
          valorProjetado: 12_000,
          custoProjetado: 7_200,
          margemProjetadaPercentual: 40,
          diferenca: 4_000,
          tendenciaPrevista: "Crescimento",
          confiancaModelo: 0.8,
        },
      ],
    });

    expect(result.evolucaoClientes[0]).toMatchObject({
      custoProjetado: 6_100,
      margemProjetadaPercentual: 39,
    });
    expect(result.evolucaoClientes[1]).toMatchObject({
      custoProjetado: 7_200,
      margemProjetadaPercentual: 40,
    });
  });

  it("detecta resposta vazia para acionar fallback", () => {
    expect(hasCustomerFinanceProjectionData({
      projecoes: {
        faturamentoMensalAtual: 0,
        proximoMes: 0,
        proximos3Meses: 0,
        proximos6Meses: 0,
        proximos12Meses: 0,
      },
      tendencias: [],
      evolucaoClientes: [],
    })).toBe(false);
  });

  it("mantem resposta real da API sem preencher tabela com demo", async () => {
    authFetchMock.mockResolvedValueOnce(new Response(JSON.stringify({
      projecoes: {
        faturamentoMensalAtual: 120000,
        proximoMes: 130000,
        proximos3Meses: 390000,
        proximos6Meses: 780000,
        proximos12Meses: 1560000,
      },
      cenarioOtimista: { margem: 15, descricao: "Otimista (+15%)" },
      cenarioRealista: { margem: 0, descricao: "Realista" },
      cenarioConservador: { margem: -15, descricao: "Conservador (-15%)" },
      tendencias: [],
      evolucaoClientes: [{
        clienteId: "CLI-API",
        clienteNome: "Cliente API",
        valorAtual: 10000,
        valorProjetado: 20000,
        diferenca: 10000,
        tendenciaPrevista: "Crescimento",
        confiancaModelo: 0.7,
      }],
    }), { status: 200 }));

    const result = await fetchCustomerFinanceProjections();

    expect(result.projecoes.proximoMes).toBe(130000);
    expect(result.tendencias).toEqual([]);
    expect(result.evolucaoClientes).toEqual([expect.objectContaining({
      clienteId: "CLI-API",
      custoProjetado: 12200,
      margemProjetadaPercentual: 39,
    })]);
  });
});
