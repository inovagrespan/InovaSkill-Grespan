import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("responsive layout behavior", () => {
  it("mantem acoes da sidebar fixas enquanto as abas rolam", () => {
    const sidebar = readSource("src/components/AppSidebar.tsx");

    expect(sidebar).toContain("custom-scrollbar min-h-0 flex-1");
    expect(sidebar).toContain("overflow-y-auto overflow-x-hidden");
    expect(sidebar).toContain("shrink-0 space-y-2 border-t");
    expect(sidebar).toContain("Sair");
    expect(sidebar).toContain("Modo claro");
    expect(sidebar).toContain("Modo escuro");
  });

  it("usa faixa horizontal de uma linha para metricas em vez de grid responsivo quebravel", () => {
    const styles = readSource("src/styles.css");
    const root = readSource("src/routes/__root.tsx");
    const dashboard = readSource("src/routes/index.tsx");
    const vendas = readSource("src/routes/vendas.tsx");
    const financas = readSource("src/routes/financas.tsx");
    const logistica = readSource("src/routes/logistica.tsx");
    const clientes = readSource("src/routes/clientes.tsx");
    const processamentos = readSource("src/routes/processamentos.tsx");

    expect(styles).toContain(".metric-row");
    expect(styles).toContain("grid-auto-flow: column");
    expect(styles).toContain("--metric-card-base-width: 248px");
    expect(styles).toContain("grid-auto-columns: var(--metric-card-column-width)");
    expect(styles).toContain("overflow-x: auto");
    expect(styles).toContain(".metric-card-item");
    expect(styles).toContain("width: var(--metric-card-base-width)");
    expect(styles).toContain("zoom: var(--metric-card-zoom)");
    expect(root).toContain("KPI_CARD_BASE_WIDTH_PX");
    expect(root).toContain("updateMetricCardZoomCompensation");
    expect(root).toContain("--metric-card-column-width");
    expect(root).toContain("--metric-card-zoom");

    for (const source of [dashboard, vendas, financas, logistica, clientes, processamentos]) {
      expect(source).toContain("metric-row");
    }
  });
});
