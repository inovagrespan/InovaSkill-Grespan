import { describe, expect, it } from "vitest";
import { buildRouteCostReport } from "./route-cost-report";

const routes = [
  { routeId: "r1", routeName: "Rota A", weekday: "MONDAY", vehicleType: "Truck 6x2", distanceMeters: 100_000, tollCost: 20, tollPassages: 1 },
  { routeId: "r2", routeName: "Rota B", weekday: "TUESDAY", vehicleType: "Truck 6x2", distanceMeters: 50_000, tollCost: 10, tollPassages: 2 },
];

describe("relatório de custos de rotas", () => {
  it("consolida combustível, pedágio e total por tipo de veículo", () => {
    const report = buildRouteCostReport(routes, 6, "vehicle");

    expect(report.routeCount).toBe(2);
    expect(report.rows).toHaveLength(1);
    expect(report.rows[0]).toMatchObject({ label: "Truck 6x2", routeCount: 2, distanceMeters: 150_000, tollCost: 30, tollPassages: 3 });
    expect(report.minimumFuelCost).toBeCloseTo(225, 2);
    expect(report.maximumFuelCost).toBeCloseTo(281.25, 2);
    expect(report.minimumTotalCost).toBeCloseTo(255, 2);
    expect(report.maximumTotalCost).toBeCloseTo(311.25, 2);
  });

  it("mantém linhas por rota e sinaliza rotas sem preço de diesel ou percurso", () => {
    const report = buildRouteCostReport([
      routes[0],
      { ...routes[1], routeId: "r3", routeName: "Rota C", distanceMeters: null },
    ], null, "route");

    expect(report.rows).toHaveLength(2);
    expect(report.rows[0].minimumTotalCost).toBeNull();
    expect(report.rows[0].unavailableRoutes).toBe(1);
    expect(report.unavailableRoutes).toBe(2);
    expect(report.tollCost).toBe(30);
  });
});
