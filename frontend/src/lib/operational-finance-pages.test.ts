import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("operational, finance and reports pages", () => {
  it("exibe métricas de controle e estoque na tela de logística", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.tsx"), "utf8");

    expect(source).toContain("Controle e Estoque");
    expect(source).toContain("Ocupação de caminhão por rota");
    expect(source).toContain("Ruptura de estoque");
    expect(source).toContain("routeOccupancy");
    expect(source).toContain("stockBreaks");
  });

  it("cria aba de finanças com filtros e métricas fictícias", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/financas.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/financas")');
    expect(source).toContain("Faturamento total");
    expect(source).toContain("Ticket médio");
    expect(source).toContain("Peso / quantidade");
    expect(source).toContain("Tempo total");
    expect(source).toContain("financeDemoTransactions");
    expect(sidebar).toContain('to: "/financas"');
    expect(sidebar).toContain('label: "Finanças"');
  });

  it("exibe médias semanal e mensal na tela de vendas", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const dashboardHelper = fs.readFileSync(path.resolve(process.cwd(), "src/lib/sales-dashboard.ts"), "utf8");

    expect(source).toContain("Média mensal");
    expect(source).toContain("Média semanal");
    expect(source).toContain("calculatePeriodAverages");
    expect(source).toContain("fetchCommercialTransactionsTimeline");
    expect(source).toContain("resolveSalesTimelineGranularity");
    expect(source).toContain("AreaChart");
    expect(source).toContain("linearGradient");
    expect(source).toContain("SALES_CHART_CARD_CLASS_NAME");
    expect(dashboardHelper).toContain("formatSalesTimelineLabel");
    expect(dashboardHelper).toContain("buildSalesTrendData");
  });

  it("mantém vendas focada em busca por nota fiscal sem gráficos agregados", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");

    expect(source).toContain('Label htmlFor="sales-document"');
    expect(source).toContain("Buscar por nota fiscal");
    expect(source).not.toContain('title="Registros"');
    expect(source).not.toContain('title="Quantidade"');
    expect(source).not.toContain("<CardTitle>Faturamento no período</CardTitle>");
    expect(source).not.toContain("<CardTitle>Ranking por empresa</CardTitle>");
  });

  it("exibe dados fictícios na tela de clientes quando não há base real", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain("DEMO_CUSTOMER_SUMMARY");
    expect(source).toContain("DEMO_CUSTOMER_RANKING");
    expect(source).toContain("sortDemoCustomers");
  });

  it("move gráficos de faturamento e ranking para clientes no modelo de área escuro", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain("Faturamento no período");
    expect(source).toContain("Ranking por empresa");
    expect(source).toContain("RevenueAreaChart");
    expect(source).toContain("AreaChart");
    expect(source).toContain("fetchCommercialTransactionsSummary");
    expect(source).toContain('bg-[#070b14]');
  });

  it("remove a aba de RH da navegação e do dashboard", () => {
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
    const dashboard = fs.readFileSync(path.resolve(process.cwd(), "src/routes/index.tsx"), "utf8");

    expect(sidebar).not.toContain('to: "/rh"');
    expect(sidebar).not.toContain("RH Atual");
    expect(dashboard).not.toContain('to="/rh"');
    expect(dashboard).not.toContain("Contexto Atual do RH");
  });

  it("transforma relatórios em emissão e impressão de métricas selecionadas", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/relatorios.tsx"), "utf8");

    expect(source).toContain("Emissão de relatórios");
    expect(source).toContain("Imprimir métricas");
    expect(source).toContain("reportAreas");
    expect(source).toContain("selectedMetrics");
    expect(source).toContain("window.print");
  });
});
