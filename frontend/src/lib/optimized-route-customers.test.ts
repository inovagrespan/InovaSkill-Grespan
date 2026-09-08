import { describe, expect, it } from "vitest";
import { groupOptimizedRouteCustomers } from "./optimized-route-customers";

describe("clientes por cidade da rota otimizada", () => {
  const routeStop = (municipalityId: string, municipalityName: string) => ({
    municipalityId, municipalityName, loadKg: 10, deliveries: 1,
    originalRouteId: "route", originalSequence: 1,
  });

  it("agrupa clientes na ordem das cidades e não repete cidade com carga dividida", () => {
    const groups = groupOptimizedRouteCustomers(
      [routeStop("city-b", "Bauru"), routeStop("city-a", "Marília"), routeStop("city-b", "Bauru")],
      {
        id: "path", name: "Rota", source: "OSRM_ROUTE_DRIVING", distanceMeters: 1, durationSeconds: 1,
        geometry: { type: "LineString", coordinates: [] },
        stops: [
          { sequence: 0, id: "depot", label: "Matriz", latitude: 0, longitude: 0, isDepot: true },
          { sequence: 1, id: "customer-b", label: "10 · Cliente B", latitude: 1, longitude: 1, isDepot: false, municipalityId: "city-b" },
          { sequence: 2, id: "customer-a", label: "20 · Cliente A", latitude: 2, longitude: 2, isDepot: false, municipalityId: "city-a" },
          { sequence: 3, id: "depot", label: "Matriz", latitude: 0, longitude: 0, isDepot: true },
        ],
      },
    );

    expect(groups.map(group => group.municipalityName)).toEqual(["Bauru", "Marília"]);
    expect(groups[0].customers).toEqual([{ id: "customer-b", label: "10 · Cliente B" }]);
    expect(groups[1].customers).toHaveLength(1);
  });

  it("mantém cidade sem cliente exato com lista vazia", () => {
    expect(groupOptimizedRouteCustomers([routeStop("city", "Garça")], null)[0].customers).toEqual([]);
  });
});
