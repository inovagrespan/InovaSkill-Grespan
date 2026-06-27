import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readRoute(routeFile: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes", routeFile), "utf8");
}

describe("fake operational KPI pages", () => {
  it("mantém Administrativo com KPIs demonstrativos no mesmo layout da Logística", () => {
    const source = readRoute("administrativo.tsx");

    expect(source).toContain('createFileRoute("/administrativo")');
    expect(source).toContain("Dados demonstrativos");
    expect(source).toContain("Saldo de Caixa Projetado");
    expect(source).toContain('import { formatKpiCompactCurrency } from "@/lib/vendas-formatters";');
    expect(source).toContain("value: formatKpiCompactCurrency(428000)");
    expect(source).toContain("Inadimplência Simulada");
    expect(source).toContain("Conformidade de Processos");
    expect(source).toContain("MetricDetailsDialog");
    expect(source).toContain('className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5"');
    expect(source).toContain('aria-label="Indicadores administrativos"');
    expect(source).toContain("Fórmula");
    expect(source).toContain("Como foi calculado");
    expect(source).toContain("Histórico do indicador");
    expect(source).toContain("Dados usados");
    expect(source).toContain("Histórico e investigação");
    expect(source).not.toContain("metric-row animate-soft-enter");
    expect(source).not.toContain("line-clamp-2 text-xs text-muted-foreground");
  });

  it("mantém Produção com KPIs demonstrativos no mesmo layout da Logística", () => {
    const source = readRoute("produtos.tsx");

    expect(source).toContain('createFileRoute("/produtos")');
    expect(source).toContain("Smart Core / Produção");
    expect(source).toContain("Dados demonstrativos");
    expect(source).toContain("Eficiência de Produção");
    expect(source).toContain("Ordens em Atraso");
    expect(source).toContain("Capacidade Disponível");
    expect(source).toContain("MetricDetailsDialog");
    expect(source).toContain('className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5"');
    expect(source).toContain('aria-label="Indicadores de produção"');
    expect(source).toContain("Fórmula");
    expect(source).toContain("Como foi calculado");
    expect(source).toContain("Histórico do indicador");
    expect(source).toContain("Dados usados");
    expect(source).toContain("Histórico e investigação");
    expect(source).not.toContain("metric-row animate-soft-enter");
    expect(source).not.toContain("line-clamp-2 text-xs text-muted-foreground");
    expect(source).not.toContain("fetchProducts");
  });
});
