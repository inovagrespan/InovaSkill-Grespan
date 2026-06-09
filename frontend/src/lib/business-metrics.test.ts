import { describe, expect, it } from "vitest";
import { calculateAverageTicket, calculateInclusivePeriodDays, calculatePeriodAverages } from "./business-metrics";

describe("business metrics", () => {
  it("calcula dias inclusivos para medias semanais e mensais", () => {
    expect(calculateInclusivePeriodDays("2026-06-01", "2026-06-07")).toBe(7);
  });

  it("calcula media semanal e mensal a partir do periodo filtrado", () => {
    const result = calculatePeriodAverages(14_000, "2026-06-01", "2026-06-14");

    expect(result.weekly).toBe(7_000);
    expect(result.monthly).toBeCloseTo(30_437.5, 1);
  });

  it("mantem as medias de vendas coerentes com o total distribuido pelos dias inclusivos", () => {
    const totalAmount = 31_000;
    const result = calculatePeriodAverages(totalAmount, "2026-05-01", "2026-05-31");

    expect(result.weekly).toBeCloseTo(totalAmount / (31 / 7), 6);
    expect(result.monthly).toBeCloseTo(totalAmount / (31 / 30.4375), 6);
  });

  it("usa um dia como periodo minimo quando datas sao invalidas ou invertidas", () => {
    const result = calculatePeriodAverages(10_000, "2026-06-10", "2026-06-01");

    expect(result.weekly).toBe(70_000);
    expect(result.monthly).toBe(304_375);
  });

  it("calcula ticket medio sem divisao invalida", () => {
    expect(calculateAverageTicket(25_000, 10)).toBe(2_500);
    expect(calculateAverageTicket(25_000, 0)).toBe(0);
  });
});
