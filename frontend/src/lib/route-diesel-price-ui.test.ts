import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("cadastro do preço do diesel em veículos", () => {
  const routeSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");
  const vehicleSource = fs.readFileSync(path.resolve(process.cwd(), "src/routes/veiculos.tipos.tsx"), "utf8");
  const apiSource = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

  it("permite consultar, editar e persistir o preço na aba de veículos", () => {
    expect(vehicleSource).toContain("Buscar média de Marília");
    expect(vehicleSource).toContain("fetchCurrentDieselPrice()");
    expect(vehicleSource).toContain("updateLogisticsFuelSettings(price)");
    expect(vehicleSource).toContain("Salvar preço");
  });

  it("usa o valor persistido na rota sem mostrar campo editável", () => {
    expect(routeSource).toContain("fetchLogisticsFuelSettings()");
    expect(routeSource).toContain("cadastrado na aba Veículos");
    expect(routeSource).not.toContain('aria-label="Preço do diesel por litro"');
    expect(apiSource).toContain("/api/vehicle-types/fuel-settings");
    expect(apiSource).toContain("/api/vehicle-types/fuel-price/research");
  });
});
