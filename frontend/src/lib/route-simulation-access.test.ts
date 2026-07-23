import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("route simulation access", () => {
  const source = fs.readFileSync(
    path.resolve(process.cwd(), "src/routes/rotas.tsx"),
    "utf8",
  );

  it("não exibe a ação individual de simulação na tela principal de rotas", () => {
    expect(source).not.toContain("RouteVehicleSimulationDialog");
    expect(source).toContain("fetchVehicleTypes()");
    expect(source).not.toContain("Simular");
    expect(source).not.toContain("openSimulation(r)");
  });

  it("mantém o apoio à decisão para vendas, logística e administradores", () => {
    const accessControl = fs.readFileSync(
      path.resolve(process.cwd(), "src/lib/access-control.ts"),
      "utf8",
    );
    expect(source).toContain("canRoleUseRouteSimulation(currentRole)");
    expect(accessControl).toContain('["vendas", "logistica", "admin", "admin_system"]');
    expect(source).toContain("{canSimulate && (");
  });
});
