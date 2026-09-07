import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import type { RouteOptimizationSummary } from "./importer-api";
import {
  dailyFleetMovement,
  dailyRouteComparison,
  calculatePercentageChange,
  formatSignedPercentage,
  suggestedRouteName,
  type SuggestedVehicle,
} from "./route-optimization-view-model";

const component = readFileSync(
  new URL("../components/RouteOptimizationSimulation.tsx", import.meta.url),
  "utf8",
);
const route = readFileSync(new URL("../routes/rotas.tsx", import.meta.url), "utf8");

function vehicle(overrides: Partial<SuggestedVehicle> = {}): SuggestedVehicle {
  return {
    id: crypto.randomUUID(),
    sequence: 0,
    isAdditional: false,
    isIdle: false,
    vehicleTypeId: crypto.randomUUID(),
    vehicleType: "Truck",
    sourceRouteId: crypto.randomUUID(),
    sourceRouteName: "Rota Centro",
    capacityKg: 10_300,
    loadKg: 8_000,
    occupancy: 8_000 / 10_300,
    distanceMeters: 1_000,
    durationSeconds: 600,
    stops: [],
    ...overrides,
  };
}

describe("sugestões otimizadas de rotas", () => {
  it("mantém data e dia compartilhados e alterna entre rotas reais e sugestões", () => {
    expect(route).toContain("Rotas reais");
    expect(route).toContain("Sugestões");
    expect(route.indexOf("RouteSnapshotDateSelect")).toBeLessThan(
      route.indexOf('routeView === "suggested"'),
    );
    expect(route).toContain('aria-label="Filtrar por dia da semana"');
    expect(route).toContain("weekday={weekday === ALL_WEEKDAYS ? undefined : weekday}");
    expect(component).toContain("fetchRouteOptimizations(date, weekday)");
    expect(component).toContain("[date, weekday]");
  });

  it("expõe cards, detalhe, histórico somente leitura, falha e polling compartilhado", () => {
    expect(component).toContain("suggestedRouteName");
    expect(component).toContain("Sequência das cidades");
    expect(component).toContain("Snapshot histórico");
    expect(component).toContain('latestExecution?.status === "Failed"');
    expect(component).toContain("rota real mantida");
    expect(component).toContain('label="Veículos em rota"');
    expect(component).toContain('label="Capacidade introduzida"');
    expect(component).toContain("OptimizationComparisonCard");
    expect(component).toContain("Movimentação da frota");
    expect(component).toContain("JOB_STATUS_POLL_INTERVAL_MS");
    expect(component).toContain("window.setInterval");
    expect(component).not.toContain("entrega(s)");
  });

  it("usa o nome da rota real e numera apenas veículos adicionais", () => {
    const real = vehicle();
    const firstAdditional = vehicle({
      sequence: 1,
      isAdditional: true,
      sourceRouteId: null,
      sourceRouteName: null,
    });
    const secondAdditional = vehicle({
      sequence: 3,
      isAdditional: true,
      sourceRouteId: null,
      sourceRouteName: null,
    });
    const idleReal = vehicle({ sequence: 2, isIdle: true, sourceRouteName: "Rota Norte" });
    const vehicles = [real, firstAdditional, idleReal, secondAdditional];

    expect(suggestedRouteName(real, vehicles)).toBe("Rota Centro");
    expect(suggestedRouteName(firstAdditional, vehicles)).toBe("Rota adicional 1");
    expect(suggestedRouteName(secondAdditional, vehicles)).toBe("Rota adicional 2");
    expect(suggestedRouteName(idleReal, vehicles)).toBe("Rota Norte");
  });

  it("mantém as métricas diárias consistentes com o contrato persistido", () => {
    const summary: RouteOptimizationSummary = {
      id: crypto.randomUUID(),
      routeImportId: crypto.randomUUID(),
      weekday: "MONDAY",
      status: "Optimized",
      reason: null,
      currentDistanceMeters: 20_000,
      proposedDistanceMeters: 15_000,
      currentDurationSeconds: 7_200,
      proposedDurationSeconds: 5_400,
      currentVehicleCount: 2,
      proposedVehicleCount: 3,
      additionalVehicleCount: 1,
      additionalCapacityKg: 3_300,
      totalWeightKg: 12_000,
      createdAt: new Date().toISOString(),
    };

    expect(dailyRouteComparison(summary)).toEqual({
      distance: { current: 20_000, proposed: 15_000, changePercent: -25 },
      duration: { current: 7_200, proposed: 5_400, changePercent: -25 },
      vehicles: { current: 2, proposed: 3, change: 1 },
      additionalVehicleCount: 1,
      additionalCapacityKg: 3_300,
      totalWeightKg: 12_000,
    });
  });

  it("calcula variação percentual, base zero, sinais e arredondamento dos comparativos", () => {
    expect(calculatePercentageChange(20_000, 15_000)).toBe(-25);
    expect(calculatePercentageChange(100, 120)).toBe(20);
    expect(calculatePercentageChange(0, 0)).toBe(0);
    expect(calculatePercentageChange(0, 100)).toBeNull();
    expect(formatSignedPercentage(-21.357)).toBe("-21,4%");
    expect(formatSignedPercentage(20)).toBe("+20%");
    expect(formatSignedPercentage(0)).toBe("0%");
    expect(formatSignedPercentage(null)).toBe("Sem base");
  });

  it("registra substituições e calcula os saldos líquidos da frota", () => {
    const vehicles = [
      vehicle({ vehicleType: "Truck", capacityKg: 10_300 }),
      vehicle({ isIdle: true, vehicleType: "Acelo", capacityKg: 3_300 }),
      vehicle({ isIdle: true, vehicleType: "Acelo", capacityKg: 3_300 }),
      vehicle({ isIdle: true, vehicleType: "Acelo", capacityKg: 3_300 }),
      vehicle({ isAdditional: true, vehicleType: "Toco", capacityKg: 7_700 }),
      vehicle({ isAdditional: true, vehicleType: "Truck", capacityKg: 10_300 }),
      vehicle({ isAdditional: true, isIdle: true, vehicleType: "Van", capacityKg: 1_500 }),
    ];

    expect(dailyFleetMovement(vehicles)).toEqual({
      releasedVehicleCount: 3,
      releasedCapacityKg: 9_900,
      introducedVehicleCount: 2,
      introducedCapacityKg: 18_000,
      netVehicleCount: -1,
      netCapacityKg: 8_100,
      releasedGroups: [{ vehicleType: "Acelo", count: 3 }],
      introducedGroups: [
        { vehicleType: "Toco", count: 1 },
        { vehicleType: "Truck", count: 1 },
      ],
    });
  });

  it("mantém zerado o resumo quando não existe substituição", () => {
    expect(dailyFleetMovement([vehicle()])).toEqual({
      releasedVehicleCount: 0,
      releasedCapacityKg: 0,
      introducedVehicleCount: 0,
      introducedCapacityKg: 0,
      netVehicleCount: 0,
      netCapacityKg: 0,
      releasedGroups: [],
      introducedGroups: [],
    });
  });
});
