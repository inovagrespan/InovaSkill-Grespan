import { describe, expect, it } from "vitest";
import { normalizeDailyOptimizationExecution } from "./importer-api";

describe("contrato do resultado da otimização diária", () => {
  it("normaliza o ResultJson persistido com propriedades PascalCase", () => {
    const execution = normalizeDailyOptimizationExecution({
      jobExecutionId: "job-1",
      status: "Completed",
      result: {
        Status: "Optimized",
        Proposed: { DistanceMeters: 12_500, DurationSeconds: 900, VehiclesUsed: 2, RentalVehicles: 1 },
        Routes: [{ Name: "Rota otimizada", DistanceMeters: 12_500, EstimatedFuelLiters: 20, TankCapacityLiters: 200, TankUsagePercent: 10, RemainingAutonomyKm: 900, RentalDailyCostMinimum: 400, RentalDailyCostMaximum: 600, Stops: [{ MunicipalityName: "Marília" }] }],
      },
      decision: { status: "PENDING" },
    });

    expect(execution.result?.proposed.distanceMeters).toBe(12_500);
    expect(execution.result?.proposed.durationSeconds).toBe(900);
    expect(execution.result?.routes[0].distanceMeters).toBe(12_500);
    expect(execution.result?.routes[0].rentalDailyCostMinimum).toBe(400);
    expect(execution.result?.routes[0].rentalDailyCostMaximum).toBe(600);
    expect(execution.result?.routes[0].tankCapacityLiters).toBe(200);
    expect(execution.result?.routes[0].tankUsagePercent).toBe(10);
    expect(execution.result?.routes[0].remainingAutonomyKm).toBe(900);
    expect(execution.result?.routes[0].stops[0].municipalityName).toBe("Marília");
  });

  it("preserva respostas que já usam camelCase", () => {
    const execution = normalizeDailyOptimizationExecution({
      jobExecutionId: "job-2",
      status: "Completed",
      result: { status: "NoImprovement", proposed: { distanceMeters: 800 }, routes: [] },
      decision: { status: "PENDING" },
    });

    expect(execution.result?.proposed.distanceMeters).toBe(800);
    expect(execution.result?.status).toBe("NoImprovement");
  });
});
