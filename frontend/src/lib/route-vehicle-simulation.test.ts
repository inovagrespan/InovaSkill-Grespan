import { describe, expect, it } from "vitest";
import { simulateRouteVehicle } from "./route-vehicle-simulation";

describe("simulateRouteVehicle", () => {
  it("calcula ocupação e diferenças usando o mesmo peso da rota", () => {
    const result = simulateRouteVehicle(4_000, 0.8, 5_000, 8_000);

    expect(result.occupancy).toBeCloseTo(0.5);
    expect(result.occupancyChange).toBeCloseTo(-0.3);
    expect(result.capacityChangeKg).toBe(3_000);
  });

  it("preserva sobrecarga para um veículo menor", () => {
    const result = simulateRouteVehicle(6_000, 0.75, 8_000, 5_000);

    expect(result.occupancy).toBeCloseTo(1.2);
    expect(result.occupancyChange).toBeCloseTo(0.45);
    expect(result.capacityChangeKg).toBe(-3_000);
  });

  it.each([null, 0, -1, Number.NaN])(
    "torna a simulação indisponível para capacidade inválida %s",
    (capacity) => {
      expect(simulateRouteVehicle(4_000, 0.8, 5_000, capacity)).toEqual({
        occupancy: null,
        occupancyChange: null,
        capacityChangeKg: null,
      });
    },
  );

  it("calcula o cenário mesmo quando a rota atual não tem capacidade", () => {
    expect(simulateRouteVehicle(4_000, null, null, 5_000)).toEqual({
      occupancy: 0.8,
      occupancyChange: null,
      capacityChangeKg: null,
    });
  });

  it("rejeita peso negativo ou não numérico", () => {
    expect(simulateRouteVehicle(-1, 0.5, 2_000, 3_000).occupancy).toBeNull();
    expect(simulateRouteVehicle(Number.NaN, 0.5, 2_000, 3_000).occupancy).toBeNull();
  });
});
