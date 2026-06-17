import { describe, expect, it } from "vitest";
import { getControlTowerScenario, sortControlTowerCardsByRisk, type ControlTowerPeriod } from "./control-tower-dashboard";

describe("control tower dashboard", () => {
  it("oferece os três períodos solicitados com cards acionáveis", () => {
    const periods: ControlTowerPeriod[] = ["today", "next7", "next30"];

    for (const period of periods) {
      const scenario = getControlTowerScenario(period);

      expect(scenario.cards.length).toBeGreaterThanOrEqual(4);
      for (const card of scenario.cards) {
        expect(card.title).not.toBe("");
        expect(card.value).not.toBe("");
        expect(["green", "yellow", "red"]).toContain(card.status);
        expect(card.description.length).toBeGreaterThan(20);
        expect(card.href).toContain("?highlight=");
        expect(["Vendas", "Produtos", "Finanças", "Logística"]).toContain(card.module);
      }
    }
  });

  it("cobre riscos operacionais, comerciais, financeiros e logísticos nas previsões", () => {
    const next7 = getControlTowerScenario("next7").cards.map((card) => card.title);
    const next30 = getControlTowerScenario("next30").cards.map((card) => card.title);

    expect(next7).toContain("Necessidade de reposição");
    expect(next7).toContain("Queda de demanda");
    expect(next7).toContain("Previsão de faturamento");
    expect(next7).toContain("Atrasos logísticos prováveis");
    expect(next30).toContain("Possível excesso de estoque");
    expect(next30).toContain("Riscos financeiros");
  });

  it("prioriza cards vermelhos antes dos amarelos e verdes", () => {
    const sorted = sortControlTowerCardsByRisk(getControlTowerScenario("today").cards);

    expect(sorted.map((card) => card.status)).toEqual(["red", "yellow", "green", "green"]);
  });
});
