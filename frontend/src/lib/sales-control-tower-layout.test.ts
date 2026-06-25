import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("sales control tower layered experience", () => {
  it("cria aba própria e mantém o fluxo legado de notas fiscais reutilizável", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
    expect(route).toContain('createFileRoute("/vendas")');
    expect(route).toContain("SalesControlTower");
    expect(route).toContain("export function VendasPage");
    expect(sidebar).toContain('{ to: "/vendas", label: "Vendas"');
  });

  it("exibe os indicadores solicitados e quatro níveis progressivos", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/SalesControlTower.tsx"), "utf8");
    for (const title of ["Volume de Vendas", "Volume de Bonificação", "Consumo de Clientes Ativos", "Consumo por Produto", "Prospecção × Fechamento", "Tempo de Conversão", "Sazonalidade de Consumo", "Taxa de Retenção", "Erro de Previsão (MAPE)", "Preço Médio por Vendedor/Região", "Ticket Médio", "Lifetime Value (LTV)"]) expect(source).toContain(title);
    expect(source).toContain("Nível 2 · Entender o resultado");
    expect(source).toContain("Nível 3 · Investigação");
    expect(source).toContain("Nível 4 · Tomada de decisão");
    expect(source).toContain("SalesTrendChart");
    expect(source).toContain("buildContextualSalesRecommendation");
    expect(source).not.toContain("LogisticsRegionMap");
  });

  it("exibe gastos e faturamento em duas linhas com filtro diário, mensal e anual", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/SalesControlTower.tsx"), "utf8");
    const styles = fs.readFileSync(path.resolve(process.cwd(), "src/styles.css"), "utf8");

    expect(source).toContain("Gastos × Faturamento");
    expect(source).toContain("text-lg font-display font-semibold leading-tight tracking-tight");
    expect(source).toContain('className="min-w-0 pt-1"');
    expect(source).toContain("border-b border-border/70 pb-4");
    expect(source).toMatch(/<Line\s+[\s\S]*?dataKey="expenses"/);
    expect(source).toMatch(/<Line\s+[\s\S]*?dataKey="revenue"/);
    expect(source).toContain('daily: "Diário"');
    expect(source).toContain('monthly: "Mensal"');
    expect(source).toContain('yearly: "Anual"');
    expect(source).toContain('aria-label="Período do gráfico financeiro"');
    expect(styles).toContain(".sales-financial-chart");
    expect(styles).toContain(".dark .sales-financial-chart");
    expect(styles).toContain("--sales-financial-revenue: #16a36a");
    expect(styles).toContain("--sales-financial-expenses: #e23b52");
    expect(source.indexOf('<section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">')).toBeLessThan(source.indexOf("<SalesRevenueExpenseChart />"));
  });

  it("compacta valores monetarios dos indicadores usando M para milhao", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/SalesControlTower.tsx"), "utf8");

    expect(source).toContain('import { formatKpiCompactCurrency } from "@/lib/vendas-formatters";');
    expect(source).toContain("formatKpiCompactCurrency(metric.revenue)");
    expect(source).toContain("formatKpiCompactCurrency(metric.bonusAmount)");
    expect(source).toContain("formatKpiCompactCurrency(metric.averageTicket)");
    expect(source).toContain("formatKpiCompactCurrency(metric.lifetimeValue)");
  });
});
