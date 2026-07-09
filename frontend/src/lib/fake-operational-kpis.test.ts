import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readRoute(routeFile: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes", routeFile), "utf8");
}

describe("fake operational KPI pages", () => {
  it("mantém Finanças com KPIs consolidados no mesmo layout da Logística", () => {
    const source = readRoute("financas.tsx");

    expect(source).toContain('createFileRoute("/financas")');
    expect(source).not.toContain("Dados demonstrativos");
    expect(source).not.toContain("Base simulada");
    expect(source).not.toContain("Todos os KPIs abaixo");
    expect(source).toContain("Faturamento total");
    expect(source).toContain('import { formatKpiCompactCurrency, formatKpiCompactNumber } from "@/lib/vendas-formatters";');
    expect(source).toContain("formatKpiCompactCurrency(metrics.totalRevenue)");
    expect(source).toContain("Pedidos");
    expect(source).toContain("Quantidade");
    expect(source).toContain("Ticket médio");
    expect(source).toContain('className="metric-row"');
    expect(source).toContain("fetchFinanceDashboard");
    expect(source).toContain("TEXT_SEARCH_DEBOUNCE_MS");
  });

  it("mantém Produção com KPIs reais no mesmo layout da Logística", () => {
    const source = readRoute("producao.tsx");

    expect(source).toContain('createFileRoute("/producao")');
    expect(source).toContain("Produção");
    expect(source).not.toContain("Dados demonstrativos");
    expect(source).not.toContain("Base simulada");
    expect(source).not.toContain("Todos os KPIs abaixo");
    expect(source).toContain("Produção (último dia)");
    expect(source).toContain("Saída (último dia)");
    expect(source).toContain("Saldo operacional");
    expect(source).toContain("Produção (mês)");
    expect(source).toContain("Saída (mês)");
    expect(source).toContain('className="metric-row"');
    expect(source).toContain("fetchProductionSummary");
    expect(source).toContain("TEXT_SEARCH_DEBOUNCE_MS");
  });
});
