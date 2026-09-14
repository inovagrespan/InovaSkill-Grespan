import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("cadastro do preço do diesel em veículos", () => {
  const vehicleSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/veiculos.tipos.tsx"), "utf8");
  const apiSource = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

  it("permite consultar, editar e persistir o preço na aba de veículos", () => {
    expect(vehicleSource).toContain("Buscar média de Marília");
    expect(vehicleSource).toContain("fetchCurrentDieselPrice()");
    expect(vehicleSource).toContain("updateLogisticsFuelSettings(price)");
    expect(vehicleSource).toContain("Salvar preço");
  });

  it("mantém os endpoints de leitura, gravação e pesquisa centralizados em veículos", () => {
    expect(apiSource).toContain("/api/vehicle-types/fuel-settings");
    expect(apiSource).toContain("/api/vehicle-types/fuel-price/research");
  });
});
