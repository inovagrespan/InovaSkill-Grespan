import { describe, expect, it } from "vitest";
import { buildContextualSalesRecommendation, buildSalesKpiHistory, calculateSalesControlSnapshot, type SalesKpiId } from "./sales-control-tower";

describe("sales control tower", () => {
  it("calcula volume, bonificação, conversão, retenção, MAPE, preço, ticket e LTV", () => {
    const metrics = calculateSalesControlSnapshot(30);
    expect(metrics.revenue).toBe(1_284_600);
    expect(metrics.weightKg).toBe(184_320);
    expect(metrics.bonusRatePercent).toBe(3.3);
    expect(metrics.conversionRatePercent).toBe(27.9);
    expect(metrics.retentionRatePercent).toBe(91.6);
    expect(metrics.mapePercent).toBe(4.5);
    expect(metrics.averagePricePerKg).toBe(18.72);
    expect(metrics.averageTicket).toBeCloseTo(4_866, 0);
    expect(metrics.lifetimeValue).toBeGreaterThan(128_000);
  });

  it("mantém séries distintas para todos os KPIs e respeita a janela temporal", () => {
    const ids: SalesKpiId[] = ["sales-volume", "bonus-volume", "active-consumption", "product-consumption", "conversion", "conversion-time", "seasonality", "retention", "mape", "average-price", "average-ticket", "ltv"];
    const histories = ids.map((id) => buildSalesKpiHistory(id, 30));
    expect(histories.every((history) => history.length === 7 && history.every((point) => point.value > 0))).toBe(true);
    expect(new Set(histories.map((history) => history.map((point) => point.value).join(","))).size).toBe(ids.length);
    expect(buildSalesKpiHistory("conversion", 1).map((point) => point.label)).toEqual(["06h", "09h", "12h", "15h", "18h", "21h", "24h"]);
    expect(buildSalesKpiHistory("retention", 7).at(-1)?.label).toBe("24/06");
  });

  it("gera recomendações específicas para o cenário comercial encontrado", () => {
    expect(buildContextualSalesRecommendation("forecast_error", "Pão Francês 60g")).toContain("Pão Francês 60g");
    expect(buildContextualSalesRecommendation("price_gap", "Região de Bauru")).toContain("Região de Bauru");
    expect(buildContextualSalesRecommendation("ltv_growth", "Padaria Santa Clara")).toContain("LTV");
  });

  it("mantém a tomada de decisão alinhada ao tema financeiro de cada causa", () => {
    const cases = [
      ["sales_growth", "capacidade de estoque"],
      ["bonus_efficiency", "bonificações"],
      ["active_consumption_growth", "mix"],
      ["product_growth", "estoque"],
      ["product_drop", "queda"],
      ["conversion_gap", "fechamento"],
      ["slow_pipeline", "tempo de resposta"],
      ["seasonality", "janela sazonal"],
      ["retention_risk", "frequência"],
      ["forecast_error", "previsão"],
      ["price_gap", "margem"],
      ["ticket_growth", "ticket"],
      ["ltv_growth", "LTV"],
    ] as const;
    for (const [cause, expectedTheme] of cases) {
      expect(buildContextualSalesRecommendation(cause, "Contexto analisado")).toContain(expectedTheme);
    }
    expect(new Set(cases.map(([cause]) => buildContextualSalesRecommendation(cause, "Contexto analisado"))).size).toBe(cases.length);
  });
});
