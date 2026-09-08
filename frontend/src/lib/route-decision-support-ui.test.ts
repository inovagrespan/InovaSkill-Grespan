import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("route decision support UI", () => {
  it("substitui o apoio à decisão pelos dados operacionais e financeiros da rota", () => {
      const source = read("src/routes/rotas.tsx");

      expect(source).not.toContain("RouteDecisionSupport");
      expect(source).not.toContain("setDetailVehicleTypes");
      expect(source).toContain('aria-label="Dados da rota"');
      expect(source).toContain("Quilômetros da rota");
      expect(source).toContain("Tempo da rota");
      expect(source).toContain("Consumo estimado");
      expect(source).toContain("Gasto estimado com combustível");
      expect(source).not.toMatch(/pedágio/i);
      expect(source).toContain("estimateFuelCost");
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
