import { describe, expect, it } from "vitest";
import {
  formatNullableCurrency,
  formatNullableCurrencyTooltip,
  formatPurchaseFrequency,
  formatVariationPercent,
  resolveComparisonColor,
  resolveRankingTrend,
  resolveRiskBadgeVariant,
  resolveCustomerStatusVariant,
} from "./customer-details";

describe("customer detail helpers", () => {
  it("formata variação positiva e negativa (fluxo feliz)", () => {
    expect(formatVariationPercent(12.345)).toBe("+12.3%");
    expect(formatVariationPercent(-2.04)).toBe("-2.0%");
  });

  it("retorna N/A para entradas inválidas (borda e inválida)", () => {
    expect(formatVariationPercent(null)).toBe("N/A");
    expect(formatVariationPercent(Number.NaN)).toBe("N/A");
  });

  it("resolve cor por tendência", () => {
    expect(resolveComparisonColor(3)).toBe("text-[var(--success)]");
    expect(resolveComparisonColor(-3)).toBe("text-[var(--danger)]");
    expect(resolveComparisonColor(0.01)).toBe("text-muted-foreground");
  });

  it("resolve variante de status", () => {
    expect(resolveCustomerStatusVariant("Em crescimento")).toBe("default");
    expect(resolveCustomerStatusVariant("Inativo")).toBe("destructive");
    expect(resolveCustomerStatusVariant("Ativo")).toBe("secondary");
  });

  it("traduz a variação da lista em uma tendência legível", () => {
    expect(resolveRankingTrend(12).label).toBe("Crescendo");
    expect(resolveRankingTrend(-8).label).toBe("Caindo");
    expect(resolveRankingTrend(2).label).toBe("Estável");
    expect(resolveRankingTrend(null).label).toBe("Sem base");
  });

  it("mostra estados explícitos para valores ausentes", () => {
    const passthrough = (value: number) => `R$ ${value.toFixed(2)}`;
    expect(formatNullableCurrency(null, passthrough)).toBe("Sem dados");
    expect(formatNullableCurrencyTooltip(null, passthrough)).toBe("Dados insuficientes no período filtrado");
  });

  it("formata frequência com histórico insuficiente e média em dias", () => {
    expect(formatPurchaseFrequency(null).value).toBe("Histórico insuficiente");
    expect(formatPurchaseFrequency(9.5).value).toBe("9.5 dias");
  });

  it("resolve estilo do badge de risco", () => {
    expect(resolveRiskBadgeVariant("Sem risco")).toBe("secondary");
    expect(resolveRiskBadgeVariant("Atenção")).toBe("outline");
    expect(resolveRiskBadgeVariant("Em risco")).toBe("default");
    expect(resolveRiskBadgeVariant("Crítico")).toBe("destructive");
  });
});
