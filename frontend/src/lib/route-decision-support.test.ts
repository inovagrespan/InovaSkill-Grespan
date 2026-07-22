import { describe, expect, it } from "vitest";
import type { ImportedRouteDetail, VehicleTypeItem } from "./importer-api";
import { buildRouteAiAnalysisPrompt, buildRouteDecisionSupport } from "./route-decision-support";

const route: ImportedRouteDetail = {
  id: "route-1", name: "Campinas → Interior", weekday: "MONDAY",
  vehicleTypeId: "truck", vehicleType: "Truck", vehicleCapacityKg: 10_000,
  totalWeightKg: 7_650, totalVolumeM3: null, totalPallets: null,
  weightOccupancy: 0.765, volumeOccupancy: null, palletOccupancy: null,
  overallOccupancy: 0.765, occupancyStatus: "Calculated", importId: "import-1",
  importVersion: 1, importFileName: "rotas.xlsx", entryCount: 2,
  totalDeliveries: 20, createdAt: "2026-07-21T00:00:00Z",
  entries: [
    { id: "city-1", sequence: 1, name: "Campinas", deliveries: 12, averagePerDay: 500, note: null },
    { id: "city-2", sequence: 2, name: "Valinhos", deliveries: 8, averagePerDay: 265, note: null },
  ],
};

const vehicles: VehicleTypeItem[] = [
  { id: "small", name: "VUC", capacityKg: 7_000, routeCount: 2 },
  { id: "toco", name: "Toco", capacityKg: 8_500, routeCount: 5 },
  { id: "truck", name: "Truck", capacityKg: 10_000, routeCount: 8 },
  { id: "large", name: "Carreta", capacityKg: 15_000, routeCount: 3 },
  { id: "invalid", name: "Sem capacidade", capacityKg: null, routeCount: 0 },
];

describe("route decision support", () => {
  it("recomenda o veículo que mantém a carga mais próxima de 90% de ocupação", () => {
    const result = buildRouteDecisionSupport(route, vehicles);

    expect(result.recommendation).toEqual(expect.objectContaining({
      vehicleTypeId: "toco",
      occupancy: 0.9,
      capacityChangeKg: -1_500,
      occupancyChange: 0.135,
      status: "Recomendado",
    }));
    expect(result.alternatives).toHaveLength(3);
    expect(result.alternatives.every((item) => item.capacityKg >= route.totalWeightKg)).toBe(true);
  });

  it("não recomenda veículo incapaz e informa quando nenhum suporta a carga", () => {
    const result = buildRouteDecisionSupport({ ...route, totalWeightKg: 20_000 }, vehicles);

    expect(result.recommendation).toBeNull();
    expect(result.alternatives).toEqual([]);
    expect(result.summary).toContain("Nenhum veículo cadastrado comporta");
  });

  it.each([0, -100, Number.NaN])("rejeita carga inválida %s sem gerar recomendação", (totalWeightKg) => {
    const result = buildRouteDecisionSupport({ ...route, totalWeightKg }, vehicles);

    expect(result.recommendation).toBeNull();
    expect(result.alternatives).toEqual([]);
    expect(result.summary).toContain("inválida ou está zerada");
  });

  it("mantém ocupação entre zero e cem por cento e sinaliza baixa margem", () => {
    const result = buildRouteDecisionSupport({ ...route, totalWeightKg: 9_800 }, vehicles);
    const truck = result.alternatives.find((item) => item.vehicleTypeId === "truck");

    expect(result.alternatives.every((item) => item.occupancy >= 0 && item.occupancy <= 1)).toBe(true);
    expect(truck).toEqual(expect.objectContaining({ occupancy: 0.98 }));
    expect(truck?.risk).toContain("Baixa margem");
  });

  it("instrui a IA a usar somente os cenários calculados sem inventar dados", () => {
    const support = buildRouteDecisionSupport(route, vehicles);
    const prompt = buildRouteAiAnalysisPrompt(route, support);

    expect(prompt).toContain("Campinas → Interior");
    expect(prompt).toContain("Toco: 8500 kg e 90.0%");
    expect(prompt).toContain("Não invente custos, distâncias ou tempos");
    expect(prompt).toContain("não aplique alterações");
    expect(prompt).toContain("até 80 palavras");
    expect(prompt).toContain("exatamente quatro linhas: Recomendação, Motivo, Risco e Próximo passo");
    expect(prompt.length).toBeLessThanOrEqual(800);
  });

  it("nunca ultrapassa 800 caracteres mesmo com nomes e muitas cidades", () => {
    const longRoute: ImportedRouteDetail = {
      ...route,
      name: "Rota extremamente longa ".repeat(20),
      vehicleType: "Veículo com descrição extensa ".repeat(10),
      entries: Array.from({ length: 100 }, (_, index) => ({
        id: `city-${index}`,
        sequence: index + 1,
        name: `Cidade com nome muito longo ${index}`,
        deliveries: 1,
        averagePerDay: 10,
        note: null,
      })),
    };
    const longVehicles = vehicles.map((vehicle) => ({ ...vehicle, name: vehicle.name.repeat(30) }));
    const prompt = buildRouteAiAnalysisPrompt(longRoute, buildRouteDecisionSupport(longRoute, longVehicles));

    expect(prompt.length).toBeLessThanOrEqual(800);
    expect(prompt).toContain("cidades: 100");
  });
});
