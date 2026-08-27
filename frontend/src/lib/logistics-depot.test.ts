import { describe, expect, it } from "vitest";
import { validateLogisticsDepotForm } from "./logistics-depot";

describe("logistics depot", () => {
  it("normaliza coordenadas pt-BR e campos textuais", () => {
    expect(validateLogisticsDepotForm({
      name: " Grespan ", address: " Avenida Principal ", latitude: "-22,217", longitude: "-49,950",
    })).toEqual({
      value: { name: "Grespan", address: "Avenida Principal", latitude: -22.217, longitude: -49.95 },
      error: null,
    });
  });

  it.each([
    [{ name: "", address: "A", latitude: "-22", longitude: "-49" }, "nome"],
    [{ name: "D", address: "", latitude: "-22", longitude: "-49" }, "endereço"],
    [{ name: "D", address: "A", latitude: "-91", longitude: "-49" }, "latitude"],
    [{ name: "D", address: "A", latitude: "-22", longitude: "181" }, "longitude"],
    [{ name: "D", address: "A", latitude: "x", longitude: "-49" }, "latitude"],
  ])("rejeita entrada inválida %#", (form, message) => {
    const result = validateLogisticsDepotForm(form);
    expect(result.value).toBeNull();
    expect(result.error).toContain(message);
  });
});
