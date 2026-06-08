import { describe, expect, it } from "vitest";
import { calculateAverageTicket, calculateInclusivePeriodDays, calculatePeriodAverages } from "./business-metrics";

describe("business metrics", () => {
  it("calcula dias inclusivos para médias semanais e mensais", () => {
    expect(calculateInclusivePeriodDays("2026-06-01", "2026-06-07")).toBe(7);
  });

  it("calcula média semanal e mensal a partir do período filtrado", () => {
    const result = calculatePeriodAverages(14_000, "2026-06-01", "2026-06-14");

    expect(result.weekly).toBe(7_000);
    expect(result.monthly).toBeCloseTo(30_437.5, 1);
  });

  it("usa um dia como período mínimo quando datas são inválidas ou invertidas", () => {
    const result = calculatePeriodAverages(10_000, "2026-06-10", "2026-06-01");

    expect(result.weekly).toBe(70_000);
    expect(result.monthly).toBe(304_375);
  });

  it("calcula ticket médio sem divisão inválida", () => {
    expect(calculateAverageTicket(25_000, 10)).toBe(2_500);
    expect(calculateAverageTicket(25_000, 0)).toBe(0);
  });
});
