import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("route optimization UI", () => {
  const routePage = fs.readFileSync(
    path.resolve(process.cwd(), "src/routes/logistica.rotas.tsx"),
    "utf8",
  );
  const sidebar = fs.readFileSync(
    path.resolve(process.cwd(), "src/components/AppSidebar.tsx"),
    "utf8",
  );
  const dialog = fs.readFileSync(
    path.resolve(process.cwd(), "src/components/RouteOptimizationDialog.tsx"),
    "utf8",
  );
  const api = fs.readFileSync(
    path.resolve(process.cwd(), "src/lib/importer-api.ts"),
    "utf8",
  );

  it("expõe sugestão global pré-processada e consulta por rota usando a data selecionada", () => {
    expect(routePage).toContain("Sugestão de IA");
    expect(routePage).toContain("fetchLatestGlobalRouteOptimization(snapshotDate)");
    expect(routePage).toContain("último job de otimização global já processado");
    expect(routePage).toContain("Plano principal recomendado");
    expect(routePage).toContain("com o cenário recomendado pronto para análise");
    expect(routePage).toContain("Análise da IA");
    expect(routePage).toContain("askBusinessAssistant(buildAssistantPlanPrompt(aiScenario))");
    expect(routePage).toContain("OccupancyMiniBar");
    expect(routePage).toContain("TabsTrigger");
    expect(routePage).toContain("Plano ideal");
    expect(routePage).toContain("Ação emergencial");
    expect(routePage).toContain("Origem melhora");
    expect(routePage).toContain("Destino permanece controlado");
    expect(routePage).toContain("Rotas redesenhadas no plano ideal");
    expect(routePage).toContain("Rota sugerida");
    expect(routePage).toContain("referenceCityName");
    expect(routePage).toContain("Cidades nesta rota");
    expect(routePage).toContain("buildLocalPlanExplanation");
    expect(routePage).toContain("Movimentos para execução manual");
    expect(routePage).not.toContain("Otimizar todas as rotas");
    expect(routePage).not.toContain("requestGlobalRouteOptimizationRun(snapshotDate)");
    expect(routePage).toContain("Ver recomendação");
    expect(routePage).toContain("RouteOptimizationDialog");
    expect(routePage).toContain("referenceDate={snapshotDate}");
    expect(routePage).toContain("openOptimization(r)");
    expect(routePage).toContain("openOptimization(selectedRoute)");
    expect(sidebar).toContain('to: "/logistica/rotas"');
  });

  it("consulta recomendação persistida por rota e não inicia job no diálogo", () => {
    expect(api).toContain("requestGlobalRouteOptimizationRun");
    expect(api).toContain("fetchLatestGlobalRouteOptimization");
    expect(api).toContain("/route-optimization-runs/latest");
    expect(api).toContain("fetchLatestRouteOptimization");
    expect(api).toContain("/latest-optimization");
    expect(dialog).toContain("Simulação: nenhuma alteração foi aplicada às rotas atuais.");
    expect(dialog).toContain("fetchLatestRouteOptimization(route.id, referenceDate)");
    expect(dialog).toContain("RECOMMENDATION_REVEAL_STEP_DELAY_MS");
    expect(dialog).toContain("Montando recomendação");
    expect(dialog).toContain("prefers-reduced-motion: reduce");
    expect(dialog).not.toContain("requestGlobalRouteOptimizationRun");
  });
});
