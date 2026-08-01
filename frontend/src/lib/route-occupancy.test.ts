import { describe, expect, it } from "vitest";
import {
  classifyOccupancy,
  formatCapacityKg,
  formatOccupancy,
  formatRouteLoadKg,
} from "./route-occupancy";

describe("route occupancy formatting", () => {
  it("formats regular occupancy", () => {
    expect(formatOccupancy(0.6)).toBe("60%");
  });

  it("preserves displayed occupancy above one hundred percent", () => {
    expect(formatOccupancy(1.25)).toBe("125%");
  });

  it("rounds only the displayed percentage to one decimal place", () => {
    expect(formatOccupancy(0.8564)).toBe("85,6%");
  });

  it("preserves up to three decimal places of route load", () => {
    expect(formatRouteLoadKg(749.192)).toBe("749,192 kg/dia");
    expect(formatRouteLoadKg(914.36)).toBe("914,36 kg/dia");
  });

  it("shows missing capacity instead of zero", () => {
    expect(formatOccupancy(null)).toBe("Capacidade não configurada");
    expect(formatCapacityKg(null)).toBe("Capacidade não configurada");
  });

  it.each([
    [0, "idle", "Ocioso"],
    [0.558182, "idle", "Ocioso"],
    [0.5999, "idle", "Ocioso"],
    [0.6, "medium", "Médio"],
    [0.8499, "medium", "Médio"],
    [0.85, "good", "Saudável"],
    [0.95, "good", "Saudável"],
    [0.9501, "critical", "Crítico"],
    [1.25, "critical", "Crítico"],
  ] as const)("classifies %s with explicit boundaries", (value, level, label) => {
    expect(classifyOccupancy(value)).toMatchObject({ level, label });
  });

  it("classifies missing or invalid occupancy as unavailable", () => {
    expect(classifyOccupancy(null).level).toBe("unavailable");
    expect(classifyOccupancy(Number.NaN).level).toBe("unavailable");
  });
});
