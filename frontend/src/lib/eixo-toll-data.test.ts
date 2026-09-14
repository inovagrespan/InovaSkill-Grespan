import { describe, expect, it } from "vitest";
import { ALL_TOLL_PLAZAS, EIXO_TOLL_PLAZAS, OTHER_TOLL_PLAZAS, automaticTollTariff, eixoAutomaticTariff } from "./logistics-map-data";

describe("catálogo de pedágios EIXO", () => {
  it("mantém as 21 praças com coordenadas e tarifas comerciais por eixo", () => {
    expect(EIXO_TOLL_PLAZAS).toHaveLength(21);
    expect(new Set(EIXO_TOLL_PLAZAS.map((plaza) => plaza.id)).size).toBe(21);
    for (const plaza of EIXO_TOLL_PLAZAS) {
      expect(plaza.lat).toBeGreaterThan(-24);
      expect(plaza.lat).toBeLessThan(-20);
      expect(plaza.lng).toBeGreaterThan(-53);
      expect(plaza.lng).toBeLessThan(-46);
      expect(Object.keys(plaza.commercialManualByAxle).map(Number)).toEqual([2, 3, 4, 5, 6, 7, 8, 9]);
      expect(plaza.commercialManualByAxle[2]).toBeGreaterThan(0);
    }
  });

  it("calcula a tarifa automática com 5% de desconto e truncamento monetário", () => {
    expect(eixoAutomaticTariff(23.3)).toBe(22.13);
    expect(eixoAutomaticTariff(3.9)).toBe(3.7);
  });

  it("mantém o catálogo complementar por concessionária", () => {
    expect(OTHER_TOLL_PLAZAS).toHaveLength(12);
    expect(ALL_TOLL_PLAZAS).toHaveLength(33);
    expect(new Set(OTHER_TOLL_PLAZAS.map((plaza) => plaza.operator))).toEqual(new Set([
      "ViaRondon", "Rodovias do Tietê", "Triunfo Transbrasiliana", "CART", "Entrevias", "Arteris ViaPaulista",
    ]));
    const marilia = OTHER_TOLL_PLAZAS.find((plaza) => plaza.id === "ENT-MARILIA")!;
    expect(marilia.commercialManualByAxle[2]).toBe(27.2);
    expect(automaticTollTariff(marilia, marilia.commercialManualByAxle[2])).toBe(25.84);
  });
});
