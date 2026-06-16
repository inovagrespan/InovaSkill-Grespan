import { describe, expect, it } from "vitest";
import { buildCustomerPeriodTrend } from "./customer-period-insights";

function point(periodStart: string, value: number) {
  return {
    periodStart,
    value,
    revenue: value,
    quantity: value,
    weight: value,
    orders: value,
    averageTicket: value,
  };
}

describe("customer period insights", () => {
  it("resume media e tendencia de crescimento no periodo", () => {
    const result = buildCustomerPeriodTrend([
      point("2025-07-01", 100),
      point("2025-08-01", 120),
      point("2025-09-01", 144),
      point("2025-10-01", 180),
    ]);

    expect(result.averageValue).toBeCloseTo(136, 3);
    expect(result.averageChangePercent).toBeCloseTo(21.67, 2);
    expect(result.totalChangePercent).toBeCloseTo(80, 3);
    expect(result.trendLabel).toBe("Crescendo");
    expect(result.tone).toBe("success");
  });

  it("identifica retracao pela leitura entre metade inicial e final", () => {
    const result = buildCustomerPeriodTrend([
      point("2025-07-01", 200),
      point("2025-08-01", 180),
      point("2025-09-01", 90),
      point("2025-10-01", 80),
    ]);

    expect(result.trendLabel).toBe("Caindo");
    expect(result.tone).toBe("danger");
  });

  it("marca estabilidade quando a oscilacao fica perto de zero", () => {
    const result = buildCustomerPeriodTrend([
      point("2025-07-01", 100),
      point("2025-08-01", 103),
      point("2025-09-01", 101),
      point("2025-10-01", 102),
    ]);

    expect(result.trendLabel).toBe("Estável");
    expect(result.tone).toBe("neutral");
  });

  it("retorna sem base quando nao ha comparacao suficiente", () => {
    const result = buildCustomerPeriodTrend([point("2025-07-01", 0)]);

    expect(result.averageValue).toBe(0);
    expect(result.averageChangePercent).toBeNull();
    expect(result.totalChangePercent).toBeNull();
    expect(result.trendLabel).toBe("Sem base");
    expect(result.comparableIntervals).toBe(0);
  });
});
