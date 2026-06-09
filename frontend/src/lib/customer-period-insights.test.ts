import { describe, expect, it } from "vitest";
import { buildCustomerPeriodTrend } from "./customer-period-insights";

describe("customer period insights", () => {
  it("resume media e tendencia de crescimento no periodo", () => {
    const result = buildCustomerPeriodTrend([
      { periodStart: "2026-03-01", value: 100, revenue: 100, quantity: 10, weight: 10, orders: 1 },
      { periodStart: "2026-04-01", value: 120, revenue: 120, quantity: 12, weight: 12, orders: 1 },
      { periodStart: "2026-05-01", value: 144, revenue: 144, quantity: 14.4, weight: 14.4, orders: 1 },
    ]);

    expect(result.averageValue).toBeCloseTo(121.33, 2);
    expect(result.averageChangePercent).toBeCloseTo(20, 3);
    expect(result.totalChangePercent).toBeCloseTo(44, 3);
    expect(result.trendLabel).toBe("Crescendo");
    expect(result.tone).toBe("success");
  });

  it("marca estabilidade quando a oscilacao fica perto de zero", () => {
    const result = buildCustomerPeriodTrend([
      { periodStart: "2026-03-01", value: 100, revenue: 100, quantity: 10, weight: 10, orders: 1 },
      { periodStart: "2026-04-01", value: 103, revenue: 103, quantity: 10.3, weight: 10.3, orders: 1 },
      { periodStart: "2026-05-01", value: 101, revenue: 101, quantity: 10.1, weight: 10.1, orders: 1 },
    ]);

    expect(result.trendLabel).toBe("Estável");
    expect(result.tone).toBe("neutral");
  });

  it("retorna sem base quando nao ha comparacao suficiente", () => {
    const result = buildCustomerPeriodTrend([
      { periodStart: "2026-03-01", value: 0, revenue: 0, quantity: 0, weight: 0, orders: 0 },
    ]);

    expect(result.averageValue).toBe(0);
    expect(result.averageChangePercent).toBeNull();
    expect(result.totalChangePercent).toBeNull();
    expect(result.trendLabel).toBe("Sem base");
    expect(result.comparableIntervals).toBe(0);
  });
});
