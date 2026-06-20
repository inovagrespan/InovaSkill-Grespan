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
    expect(source).not.toContain("Modo do gráfico de receita");
    expect(source).toContain('SALES_CHART_HEIGHT_CLASS_NAME = "h-[var(--dashboard-chart-height)] min-h-[var(--dashboard-chart-height)]"');
    expect(source).toContain("SALES_ANALYTICS_PANEL_CLASS_NAME");
    expect(source).toContain("border-border/70");
    expect(source).toContain("bg-[var(--surface-soft)]/70");
    expect(source).toContain("sales-chart-card");
    expect(source).toContain("SALES_REVENUE_FILL");
    expect(source).toContain("SALES_REVENUE_FILL_END");
    expect(source).toContain('type="monotone"');
    expect(source).toContain("stroke={SALES_REVENUE_COLOR}");
    expect(source).toContain("strokeWidth={2.8}");
    expect(source).toContain("dot={{ r: 3, fill: SALES_REVENUE_COLOR, strokeWidth: 0 }}");
    expect(source).toContain("cursor={{ stroke: SALES_CURSOR_STROKE, strokeWidth: 2 }}");
    expect(source).toContain("stopColor={SALES_REVENUE_FILL}");
    expect(source).toContain("stopColor={SALES_REVENUE_FILL_END}");
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
    expect(routeSource).toContain("h-9 rounded-[var(--dashboard-control-radius)] border border-[var(--sales-chart-select-border)] bg-[var(--sales-chart-select-bg)]");
    expect(routeSource).toContain('value={companySortBy}');
    expect(routeSource).toContain("DEFAULT_SUMMARY_GRANULARITY");
    expect(routeSource).not.toContain('value={summaryGranularity}');
    expect(routeSource).not.toContain("setSummaryGranularity");
    expect(routeSource).toContain("<div className={SALES_ANALYTICS_PANEL_CLASS_NAME}>");
    expect(routeSource).not.toContain("bg-[#070b14]");
    expect(routeSource).not.toContain("border-[#182033]");
    expect(routeSource).not.toContain("border-white/6");
  });

  it("define contraste claro e escuro para o grafico de receita de vendas", () => {
    const styles = read("src/styles.css");

    expect(styles).toContain(".sales-chart-card");
    expect(styles).toContain(".dark .sales-chart-card");
    expect(styles).toContain("--sales-chart-line: #ff3158");
    expect(styles).toContain("--sales-chart-fill: rgba(255, 49, 88, 0.34)");
    expect(styles).toContain("--sales-chart-axis: #98a4b8");
  });
});
