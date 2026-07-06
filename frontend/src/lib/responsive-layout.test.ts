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
    expect(sidebar).toContain("fixed inset-y-0 left-0 z-40 hidden h-dvh");
    expect(sidebar).toContain("sticky bottom-0 z-10 shrink-0 border-t");
    expect(sidebar).toContain("mb-3 mt-4 flex shrink-0 items-center gap-3 px-3 pb-4");
    expect(sidebar).toContain('collapsed ? "justify-center" : "justify-between border-b border-border"');
    expect(sidebar).toContain("flex size-10 shrink-0 items-center justify-center rounded-full");
    expect(sidebar).toContain("transition-[width] duration-300 ease-out");
    expect(sidebar).toContain("rounded-full");
    expect(sidebar).toContain("Sair");
    expect(sidebar).toContain("Modo claro");
    expect(sidebar).toContain("Modo escuro");
  });

  it("usa faixa horizontal de uma linha para metricas em vez de grid responsivo quebravel", () => {
    const styles = readSource("src/styles.css");
    const root = readSource("src/routes/__root.tsx");
    const dashboard = readSource("src/routes/dashboard.tsx");
    const vendas = readSource("src/routes/vendas.tsx");
    const financas = readSource("src/routes/financas.tsx");
    const logistica = readSource("src/routes/logistica.index.tsx");
    const clientes = readSource("src/routes/clientes.tsx");
    const processamentos = readSource("src/routes/processamentos.tsx");

    expect(styles).toContain(".metric-row");
    expect(styles).toContain(".page-shell");
    expect(styles).toContain(".dark .page-shell");
    expect(styles).toContain("radial-gradient(900px circle at 0% 0%, rgba(180, 35, 47");
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
    expect(root).toContain('useState<"light" | "dark">("dark")');
    expect(root).toContain('setTheme("dark")');

    for (const source of [dashboard, vendas, financas, processamentos]) {
      expect(source).toContain("metric-row");
    }
    expect(clientes).toContain("SkeletonTable");
    expect(logistica).toContain("grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5");
    expect(logistica).toContain("Indicadores logísticos");
  });
});
