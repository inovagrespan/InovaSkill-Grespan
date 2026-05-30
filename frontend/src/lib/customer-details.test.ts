import { describe, expect, it } from "vitest";
import {
  formatNullableCurrency,
  formatNullableCurrencyTooltip,
  formatPurchaseFrequency,
  formatVariationPercent,
  resolveComparisonColor,
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

  it("mostra estados explicitos para valores ausentes", () => {
    const passthrough = (value: number) => `R$ ${value.toFixed(2)}`;
    expect(formatNullableCurrency(null, passthrough)).toBe("Sem dados");
    expect(formatNullableCurrencyTooltip(null, passthrough)).toBe("Dados insuficientes no período filtrado");
  });

  it("formata frequência com compra única e média em dias", () => {
    expect(formatPurchaseFrequency(null).value).toBe("Compra única no período");
    expect(formatPurchaseFrequency(9.5).value).toBe("10 dias");
  });
});
