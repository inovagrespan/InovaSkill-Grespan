import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("route simulation access", () => {
  const source = fs.readFileSync(
    path.resolve(process.cwd(), "src/routes/rotas.tsx"),
    "utf8",
  );

  it("mantém a simulação na tela principal de rotas", () => {
    expect(source).toContain("RouteVehicleSimulationDialog");
    expect(source).toContain("fetchVehicleTypes()");
    expect(source).toContain("Simular");
    expect(source).toContain("openSimulation(r)");
  });

  it("exibe o botão para vendas, logística e administradores", () => {
    expect(source).toContain('currentRole === "vendas"');
    expect(source).toContain('currentRole === "logistica"');
    expect(source).toContain('currentRole === "admin"');
    expect(source).toContain('currentRole === "admin_system"');
    expect(source).toContain("{canSimulate && (");
  });
});
