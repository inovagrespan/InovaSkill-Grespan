import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("sales chart refinement", () => {
  it("mantem os graficos no mesmo sistema visual do dashboard", () => {
    const source = read("src/routes/vendas.tsx");

    expect(source).toContain("Evolução da receita");
    expect(source).toContain("Ranking por empresa");
    expect(source).toContain("Modo do gráfico de receita");
    expect(source).toContain('SALES_CHART_HEIGHT_CLASS_NAME = "h-[var(--dashboard-chart-height)] min-h-[var(--dashboard-chart-height)]"');
    expect(source).toContain("SALES_ANALYTICS_PANEL_CLASS_NAME");
    expect(source).toContain("border-border/70");
    expect(source).toContain("bg-[var(--surface-soft)]/70");
    expect(source).toContain('type="monotone"');
    expect(source).toContain("stroke={SALES_REVENUE_COLOR}");
    expect(source).toContain("dot={false}");
    expect(source).toContain("cursor={{ stroke: SALES_CURSOR_STROKE, strokeWidth: 1 }}");
    expect(source).toContain("stopColor={SALES_REVENUE_COLOR}");
  });

  it("exibe tooltip com a mesma superficie clara dos demais componentes", () => {
    const routeSource = read("src/routes/vendas.tsx");
    const helperSource = read("src/lib/sales-dashboard.ts");

    expect(routeSource).toContain("SalesRevenueTooltip");
    expect(routeSource).toContain("bg-surface");
    expect(routeSource).toContain("rounded-lg");
    expect(helperSource).toContain("tooltipLabel");
    expect(helperSource).toContain("T00:00:00Z");
  });

  it("mantem import do ChartTooltipContent quando o ranking reutiliza esse tooltip", () => {
    const routeSource = read("src/routes/vendas.tsx");

    expect(routeSource).toContain('import { ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";');
    expect(routeSource).toContain("<ChartTooltipContent");
  });

  it("padroniza selects e evita ilhas escuras hardcoded nos graficos", () => {
    const routeSource = read("src/routes/vendas.tsx");

    expect(routeSource).toContain("SALES_CHART_SELECT_CLASS_NAME");
    expect(routeSource).toContain("h-9 rounded-[var(--dashboard-control-radius)] border border-input bg-surface");
    expect(routeSource).toContain("<div className={SALES_ANALYTICS_PANEL_CLASS_NAME}>");
    expect(routeSource).not.toContain("bg-[#070b14]");
    expect(routeSource).not.toContain("border-[#182033]");
    expect(routeSource).not.toContain("border-white/6");
  });
});
