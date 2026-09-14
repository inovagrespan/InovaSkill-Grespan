import { describe, expect, it } from "vitest";
import { estimateFuelCost, estimateRouteFuelConsumption, formatRouteDuration, formatRouteReturnTime, getRouteReturnWindowStatus, totalRouteFuelConsumption } from "./route-fuel-consumption";

describe("estimativa de combustível das rotas", () => {
  it.each([
    ["Mercedes Accelo", 5.5, 7],
    ["Toco (4x2)", 3.8, 4.5],
    ["Truck (6x2)", 3.2, 4],
  ])("aplica a faixa cadastrada para %s", (vehicleType, minimumEfficiency, maximumEfficiency) => {
    const estimate = estimateRouteFuelConsumption(vehicleType, 100_000)!;
    expect(estimate.minimumLiters).toBeCloseTo(100 / maximumEfficiency, 8);
    expect(estimate.maximumLiters).toBeCloseTo(100 / minimumEfficiency, 8);
    expect(estimate.minimumEfficiencyKmPerLiter).toBe(minimumEfficiency);
    expect(estimate.maximumEfficiencyKmPerLiter).toBe(maximumEfficiency);
  });

  it("reconhece nomes operacionais com marca, caixa e pontuação diferentes", () => {
    expect(estimateRouteFuelConsumption("Acelo", 55_000)?.maximumLiters).toBe(10);
    expect(estimateRouteFuelConsumption("MB ACCELO 1016", 55_000)?.maximumLiters).toBe(10);
    expect(estimateRouteFuelConsumption("Caminhão TOCO-4X2", 45_000)?.minimumLiters).toBe(10);
    expect(estimateRouteFuelConsumption("TRUCK", 40_000)?.minimumLiters).toBe(10);
  });

  it("retorna zero para percurso zerado e indisponível para tipo ou distância inválidos", () => {
    expect(estimateRouteFuelConsumption("Truck", 0)?.maximumLiters).toBe(0);
    expect(estimateRouteFuelConsumption("Van", 10_000)).toBeNull();
    expect(estimateRouteFuelConsumption("Truck", -1)).toBeNull();
    expect(estimateRouteFuelConsumption("Truck", Number.NaN)).toBeNull();
  });

  it("soma as partes e informa quantas rotas ficaram sem referência", () => {
    const total = totalRouteFuelConsumption([
      { vehicleType: "Truck", distanceMeters: 40_000 },
      { vehicleType: "Toco", distanceMeters: 45_000 },
      { vehicleType: "Van", distanceMeters: 20_000 },
    ]);

    expect(total.minimumLiters).toBe(20);
    expect(total.maximumLiters).toBeCloseTo(40 / 3.2 + 45 / 3.8, 8);
    expect(total.unavailableRoutes).toBe(1);
  });

  it.each([
    [529 * 60, "8 h 49 min"],
    [45 * 60, "45 min"],
    [2 * 60 * 60, "2 h"],
    [89, "1 min"],
  ])("formata %s segundos em horas e minutos", (seconds, expected) => {
    expect(formatRouteDuration(seconds)).toBe(expected);
  });

  it("não formata duração negativa ou inválida", () => {
    expect(formatRouteDuration(-1)).toBe("Indisponível");
    expect(formatRouteDuration(Number.NaN)).toBe("Indisponível");
  });

  it.each([
    [10 * 60 * 60, "18:00", "preferred"],
    [10.5 * 60 * 60, "18:30", "tolerance"],
    [11 * 60 * 60, "19:00", "tolerance"],
    [11 * 60 * 60 + 60, "19:01", "late"],
  ])("calcula retorno a partir das 08:00 para %s segundos", (seconds, returnTime, status) => {
    expect(formatRouteReturnTime(seconds)).toBe(returnTime);
    expect(getRouteReturnWindowStatus(seconds)).toBe(status);
  });

  it("calcula o gasto de combustível preservando mínimo e máximo", () => {
    const fuel = estimateRouteFuelConsumption("Truck", 100_000);
    expect(estimateFuelCost(fuel, 6)).toEqual({ minimumFuelCost: 150, maximumFuelCost: 187.5 });
  });

  it("não calcula gasto de combustível com preço ausente ou inválido", () => {
    const fuel = estimateRouteFuelConsumption("Truck", 100_000);
    expect(estimateFuelCost(null, 6)).toBeNull();
    expect(estimateFuelCost(fuel, null)).toBeNull();
    expect(estimateFuelCost(fuel, -1)).toBeNull();
    expect(estimateFuelCost(fuel, Number.NaN)).toBeNull();
  });
});
