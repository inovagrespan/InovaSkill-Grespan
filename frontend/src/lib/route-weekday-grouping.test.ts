import { describe, expect, it } from "vitest";
import { groupRoutesByWeekday } from "@/lib/route-weekday";
import type { ImportedRouteItem } from "@/lib/importer-api";

const route = (id: string, weekday: string): ImportedRouteItem => ({
  id, name: `Rota ${id}`, weekday, vehicleTypeId: "vehicle-type", vehicleType: "Truck",
  vehicleCapacityKg: 10_000, totalWeightKg: 100, totalVolumeM3: null, totalPallets: null,
  weightOccupancy: 1, volumeOccupancy: null, palletOccupancy: null, overallOccupancy: 1,
  occupancyStatus: "Calculated", importId: "import", importVersion: 1,
  importFileName: "rotas.xlsx", entryCount: 1, totalDeliveries: 1,
  createdAt: "2026-09-01T00:00:00Z",
});

describe("agrupamento de rotas por dia da semana", () => {
  it("ordena os dias úteis e preserva as rotas de cada grupo", () => {
    const groups = groupRoutesByWeekday([
      route("sexta", "FRIDAY"), route("segunda-1", "MONDAY"),
      route("terça", "TUESDAY"), route("segunda-2", "MONDAY"),
    ]);
    expect(groups.map((group) => group.weekday)).toEqual(["MONDAY", "TUESDAY", "FRIDAY"]);
    expect(groups[0].routes.map((item) => item.id)).toEqual(["segunda-1", "segunda-2"]);
  });

  it("não cria grupos vazios", () => {
    expect(groupRoutesByWeekday([])).toEqual([]);
  });
});
