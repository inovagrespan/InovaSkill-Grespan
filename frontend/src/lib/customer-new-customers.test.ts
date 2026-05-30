import { describe, expect, it } from "vitest";
import { computeNewCustomersInsights } from "./customer-new-customers";

describe("computeNewCustomersInsights", () => {
  it("retorna zeros quando não há pontos", () => {
    const result = computeNewCustomersInsights([]);
    expect(result.totalMonths).toBe(0);
    expect(result.averagePerMonth).toBe(0);
    expect(result.peakMonthLabel).toBe("N/A");
    expect(result.peakMonthValue).toBe(0);
  });

  it("calcula média mensal e pico corretamente", () => {
    const result = computeNewCustomersInsights([
      { monthStart: "2026-01-01T00:00:00Z", newCustomers: 3 },
      { monthStart: "2026-02-01T00:00:00Z", newCustomers: 1 },
      { monthStart: "2026-03-01T00:00:00Z", newCustomers: 5 },
    ]);

    expect(result.totalMonths).toBe(3);
    expect(result.averagePerMonth).toBe(3);
    expect(result.peakMonthValue).toBe(5);
  });
});
