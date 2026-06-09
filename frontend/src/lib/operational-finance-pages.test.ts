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

  it("mantem rota de finanças com filtros, métricas e paginação vindas da API", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/financas.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
    const styles = fs.readFileSync(path.resolve(process.cwd(), "src/styles.css"), "utf8");

    expect(source).toContain('createFileRoute("/financas")');
    expect(source).toContain("Faturamento total");
    expect(source).toContain("Ticket médio");
    expect(source).toContain("Peso / quantidade");
    expect(source).toContain("Tempo total");
    expect(source).toContain("fetchFinanceDashboard");
    expect(source).toContain("fetchFinanceCustomers");
    expect(source).toContain("useDebouncedValue(customerSearch, CUSTOMER_SEARCH_DEBOUNCE_MS)");
    expect(source).toContain("CUSTOMER_SEARCH_DEBOUNCE_MS = 300");
    expect(source).toContain("new AbortController()");
    expect(source).toContain("customerSearchRequestId");
    expect(source).toContain("Buscando clientes...");
    expect(source).toContain("Nenhum cliente encontrado.");
    expect(source).toContain("Todos os clientes");
    expect(source).not.toContain("<datalist");
    expect(source).toContain("Evolução da Receita");
    expect(source).toContain("Ranking por empresa");
    expect(source).toContain("setPage(1)");
    expect(source).toContain("Página {currentPage} de {totalPages}");
    expect(source).toContain("Anterior");
    expect(source).toContain("Próxima");
    expect(source).toContain("ChevronLeft");
    expect(source).toContain("ChevronRight");
    expect(source).toContain("revenueGranularityOptions");
    expect(source).toContain("AreaChart");
    expect(source).toContain("Base financeira filtrada");
    expect(source).not.toContain("financeDemoTransactions");
    expect(styles).toContain(".finance-chart-card");
    expect(styles).toContain(".dark .finance-chart-card");
    expect(sidebar).not.toContain('to: "/financas"');
    expect(sidebar).not.toContain('label: "Finanças"');
  });

  it("mescla clientes e finanças com métricas financeiras no topo e lista de clientes abaixo", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(source).toContain("fetchFinanceDashboard");
    expect(source).toContain("financeMetrics.totalRevenue");
    expect(source).toContain("Métrica financeira consolidada pelos filtros");
    expect(source).toContain("Evolução da Receita");
    expect(source).toContain("Ranking por empresa");
    expect(source).toContain("financeRevenueTrendData");
    expect(source).toContain("financeCustomerRankingData");
    expect(source).toContain("RevenueAreaChart");
    expect(source).toContain("<CardTitle>Clientes</CardTitle>");
    expect(source).toContain("Clique em um cliente para abrir a tela de detalhes.");
    expect(source).toContain("onClick={() => openDetails(item.customerCode)}");
    expect(sidebar).toContain('to: "/clientes"');
    expect(sidebar).toContain('label: "Clientes"');
    expect(sidebar).not.toContain('to: "/financas"');
  });

  it("cria aba de produtos para consultar produtos cadastrados", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/produtos.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/produtos")');
    expect(source).toContain("Produtos cadastrados");
    expect(source).toContain("SKU ou descrição do produto");
    expect(source).toContain("fetchProducts");
    expect(source).toContain("useDebouncedValue(search, PRODUCT_SEARCH_DEBOUNCE_MS)");
    expect(source).toContain("PRODUCT_SEARCH_DEBOUNCE_MS = 300");
    expect(source).toContain("new AbortController()");
    expect(source).toContain("Nenhum produto encontrado.");
    expect(source).toContain("Página {page} de {totalPages}");
    expect(sidebar).toContain('to: "/produtos"');
    expect(sidebar).toContain('label: "Produtos"');
    expect(sidebar).toContain("PackageSearch");
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

  it("mantém clientes como lista operacional com detalhe em modal", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain("<TableHead>Cliente</TableHead>");
    expect(source).toContain("<TableHead>Faturamento</TableHead>");
    expect(source).toContain("DialogTitle>Detalhes do Cliente");
    expect(source).toContain("loadCustomerDetails");
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
