import { describe, expect, it } from "vitest";
import { groupRouteCostItems } from "./route-cost-view";
import type { RouteCostItem } from "./importer-api";

function item(overrides: Partial<RouteCostItem>): RouteCostItem {
  return { id: "1", routeId: "r1", optimizationResultId: null, optimizationVehicleId: null, vehicleTypeId: "v1", vehicleType: "Truck", label: "Rota 1", weekday: "MONDAY", pathBasis: "ExactCustomerCoordinates", isAvailable: true, unavailableReason: null, distanceMeters: 100, durationSeconds: 10, minimumFuelLiters: 2, maximumFuelLiters: 3, minimumFuelCost: 12, maximumFuelCost: 18, tollCost: 5, tollPassages: 1, minimumTotalCost: 17, maximumTotalCost: 23, tolls: [], ...overrides };
}

describe("agrupamento visual dos custos oficiais", () => {
  it("soma somente valores persistidos ao agrupar por veículo", () => {
    const rows = groupRouteCostItems([item({}), item({ id: "2", routeId: "r2", distanceMeters: 200, minimumFuelCost: 24, maximumFuelCost: 36, tollCost: 10, minimumTotalCost: 34, maximumTotalCost: 46 })], "vehicle");
    expect(rows[0]).toMatchObject({ label: "Truck", routeCount: 2, distanceMeters: 300, minimumFuelCost: 36, maximumFuelCost: 54, tollCost: 15, minimumTotalCost: 51, maximumTotalCost: 69 });
  });

  it("não inclui item indisponível nos subtotais e preserva sua contagem", () => {
    const rows = groupRouteCostItems([item({}), item({ id: "2", isAvailable: false, distanceMeters: null, minimumFuelCost: null, maximumFuelCost: null, tollCost: 99, minimumTotalCost: null, maximumTotalCost: null })], "vehicle");
    expect(rows[0]).toMatchObject({ routeCount: 2, unavailableItemCount: 1, tollCost: 5, minimumTotalCost: 17 });
  });

  it("mantém valores nulos quando todo o grupo está indisponível", () => {
    const rows = groupRouteCostItems([item({ isAvailable: false, distanceMeters: null, minimumFuelCost: null, maximumFuelCost: null, minimumTotalCost: null, maximumTotalCost: null })], "vehicle");
    expect(rows[0]).toMatchObject({ isAvailable: false, distanceMeters: null, minimumFuelCost: null, minimumTotalCost: null });
  });
});
