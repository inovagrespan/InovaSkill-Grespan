import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("configuração de custo por tipo de veículo", () => {
  const page = fs.readFileSync(path.resolve(process.cwd(), "src/routes/veiculos.tipos.tsx"), "utf8");
  const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

  it("permite consultar e editar eixos e faixa de consumo", () => {
    expect(page).toContain("Quantidade de eixos");
    expect(page).toContain("Consumo mínimo (km/L)");
    expect(page).toContain("Consumo máximo (km/L)");
    expect(page).toContain("validateVehicleTypeForm");
    expect(page).toContain("Consumo não configurado");
  });

  it("envia os campos operacionais no contrato de criação e edição", () => {
    expect(api).toContain("axleCount: number | null");
    expect(api).toContain("minimumFuelEfficiencyKmPerLiter: number | null");
    expect(api).toContain("maximumFuelEfficiencyKmPerLiter: number | null");
    expect(api).toContain("body: JSON.stringify(input)");
  });
});
