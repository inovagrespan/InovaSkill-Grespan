import { describe, expect, it } from "vitest";
import { getControlTowerScenario, sortControlTowerCardsByRisk, type ControlTowerPeriod } from "./control-tower-dashboard";

describe("control tower dashboard", () => {
  it("oferece os três períodos solicitados com cards acionáveis", () => {
    const periods: ControlTowerPeriod[] = ["today", "next7", "next30"];

    for (const period of periods) {
      const scenario = getControlTowerScenario(period);

      expect(scenario.cards.length).toBeGreaterThanOrEqual(2);
      for (const card of scenario.cards) {
        expect(card.title).not.toBe("");
        expect(card.value).not.toBe("");
        expect(["yellow", "red"]).toContain(card.status);
        expect(card.description.length).toBeGreaterThan(20);
        expect(card.href).toContain("highlight=");
        expect(["Vendas", "Produtos", "Finanças", "Logística"]).toContain(card.module);
      }
    }
  });

  it("cobre riscos operacionais, comerciais e logísticos nas previsões", () => {
    const next7 = getControlTowerScenario("next7").cards.map((card) => card.title);
    const next30 = getControlTowerScenario("next30").cards.map((card) => card.title);

    expect(next7).toContain("Necessidade de reposição");
    expect(next7).toContain("Queda de demanda");
    expect(next7).toContain("Atrasos logísticos prováveis");
    expect(next30).toContain("Possível excesso de estoque");
  });

  it("prioriza cards vermelhos antes dos amarelos e verdes", () => {
    const sorted = sortControlTowerCardsByRisk(getControlTowerScenario("today").cards);

    expect(sorted.map((card) => card.status)).toEqual(["red", "yellow", "yellow"]);
  });

  it("remove cards saudaveis para manter somente urgencias", () => {
    for (const period of ["today", "next7", "next30"] as const) {
      expect(getControlTowerScenario(period).cards.every((card) => card.status !== "green")).toBe(true);
    }
  });

  it("direciona avisos comerciais para a aba atual de clientes", () => {
    const allCards = [
      ...getControlTowerScenario("today").cards,
      ...getControlTowerScenario("next7").cards,
      ...getControlTowerScenario("next30").cards,
    ];

    expect(allCards.map((card) => card.href)).toContain("/clientes?aba=impacto&highlight=demand-drop-7d");
  });

  it("não exibe KPIs de Finanças em nenhum período", () => {
    for (const period of ["today", "next7", "next30"] as const) {
      expect(getControlTowerScenario(period).cards.some((card) => card.module === "Finanças")).toBe(false);
    }
  });

  it("exibe o KPI de conversão de Vendas com os dados da carteira comercial", () => {
    const salesCard = getControlTowerScenario("today").cards.find((card) => card.id === "sales-conversion-today");

    expect(salesCard).toEqual(expect.objectContaining({
      module: "Vendas",
      value: "24,8%",
      status: "yellow",
      href: "/vendas?highlight=sales-conversion-today",
    }));
    expect(salesCard?.description).toContain("meta de 32%");
    expect(salesCard?.description).toContain("18 propostas");
  });
});
