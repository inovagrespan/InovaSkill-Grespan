import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("route decision support UI", () => {
  it("carrega e exibe o apoio à decisão ao abrir detalhes", () => {
      const source = read("src/routes/rotas.tsx");

      expect(source).toContain("RouteDecisionSupport");
      expect(source).toContain("setDetailVehicleTypes(await fetchVehicleTypes())");
      expect(source).toContain("decisionSupportError");
      expect(source).toContain("route={selectedRoute}");
      expect(source).toContain("canRoleUseRouteSimulation");
      expect(source).toContain("{canSimulate && (");
  });

  it("mantém análise por IA opcional, explicável e sem alteração automática", () => {
    const component = read("src/components/RouteDecisionSupport.tsx");

    expect(component).toContain("buildRouteDecisionSupport");
    expect(component).toContain("buildRouteAiAnalysisPrompt");
    expect(component).toContain("askBusinessAssistant");
    expect(component).toContain("Analisar com IA");
    expect(component).toContain("Resumo da IA");
    expect(component).toContain("Nenhuma alteração é aplicada automaticamente");
    expect(component).toContain("aiLoading");
    expect(component).toContain("aiError");
  });

  it("usa a mesma autorização para simulação, cálculo e análise por IA", () => {
    const accessControl = read("src/lib/access-control.ts");

    expect(accessControl).toContain('const ROUTE_SIMULATION_ROLES: readonly ApplicationRole[] = ["vendas", "logistica", "admin", "admin_system"]');
    expect(accessControl).toContain("export function canRoleUseRouteSimulation");
  });
});
