import { describe, expect, it } from "vitest";
import { EIXO_TOLL_PLAZAS, OTHER_TOLL_PLAZAS } from "./logistics-map-data";
import { estimateRouteTollCost } from "./route-toll-cost";

describe("custo de pedágio da rota", () => {
  it("identifica a praça atravessada e calcula cada passagem no automático", () => {
    const plaza = EIXO_TOLL_PLAZAS.find((item) => item.id === "P01")!;
    const coordinates: [number, number][] = [
      [plaza.lng - 0.02, plaza.lat],
      [plaza.lng + 0.02, plaza.lat],
    ];

    const result = estimateRouteTollCost(coordinates);

    expect(result.totalPassages).toBe(1);
    expect(result.tolls).toHaveLength(1);
    expect(result.tolls[0].plaza.id).toBe("P01");
    expect(result.totalAutomaticCost).toBe(22.13);
  });

  it("conta ida e retorno como duas passagens, sem duplicar segmentos consecutivos", () => {
    const plaza = EIXO_TOLL_PLAZAS.find((item) => item.id === "P21")!;
    const coordinates: [number, number][] = [
      [plaza.lng - 0.05, plaza.lat],
      [plaza.lng, plaza.lat],
      [plaza.lng + 0.05, plaza.lat],
      [plaza.lng + 0.05, plaza.lat + 0.05],
      [plaza.lng, plaza.lat],
      [plaza.lng - 0.05, plaza.lat],
    ];

    const result = estimateRouteTollCost(coordinates);

    expect(result.totalPassages).toBe(2);
    expect(result.tolls[0].passages).toBe(2);
    expect(result.totalAutomaticCost).toBe(14.62);
  });

  it("retorna custo zero quando a geometria não passa por nenhuma praça", () => {
    const result = estimateRouteTollCost([[-49.94583, -22.21389], [-49.95, -22.22]]);

    expect(result).toEqual({ tolls: [], totalAutomaticCost: 0, totalPassages: 0 });
  });

  it("identifica uma praça complementar e aplica a categoria de 2 eixos", () => {
    const plaza = OTHER_TOLL_PLAZAS.find((item) => item.id === "CART-PIRATININGA")!;
    const coordinates: [number, number][] = [
      [plaza.lng - 0.02, plaza.lat],
      [plaza.lng + 0.02, plaza.lat],
    ];

    const result = estimateRouteTollCost(coordinates, 2);

    expect(result.totalPassages).toBe(1);
    expect(result.tolls[0].plaza.operator).toBe("CART");
    expect(result.tolls[0].plaza.commercialManualByAxle[2]).toBe(21.4);
    expect(result.totalAutomaticCost).toBe(20.33);

    const threeAxleResult = estimateRouteTollCost(coordinates, 3);
    expect(threeAxleResult.tolls[0].plaza.commercialManualByAxle[3]).toBe(32.1);
    expect(threeAxleResult.totalAutomaticCost).toBe(30.49);
  });
});
