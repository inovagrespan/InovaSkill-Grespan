import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("operational, finance and reports pages", () => {
  it("transforma o dashboard em torre de controle com períodos, previsões e cards clicáveis", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/index.tsx"), "utf8");
    const helper = fs.readFileSync(path.resolve(process.cwd(), "src/lib/control-tower-dashboard.ts"), "utf8");

    expect(source).toContain("Torre de Controle Inteligente");
    expect(source).toContain("periodOptions");
    expect(source).toContain("Hoje");
    expect(source).toContain("Próximos 7 dias");
    expect(source).toContain("Próximos 30 dias");
    expect(source).toContain("sortControlTowerCardsByRisk");
    expect(source).toContain("statusLabel");
    expect(source).toContain("href={card.href}");
    expect(helper).toContain("Produtos com risco de ruptura");
    expect(helper).toContain("Necessidade de reposição");
    expect(helper).toContain("Queda de demanda");
    expect(helper).toContain("Possível excesso de estoque");
    expect(helper).toContain("Previsão de faturamento");
    expect(helper).toContain("Atrasos logísticos prováveis");
    expect(helper).toContain("Riscos financeiros");
    expect(helper).toContain("/clientes?aba=nota-fiscal&highlight=");
    expect(helper).toContain("/clientes?aba=impacto&highlight=");
    expect(helper).toContain("/produtos?highlight=");
    expect(helper).toContain("/clientes?aba=projecoes&highlight=");
    expect(helper).toContain("/logistica?highlight=");
  });

  it("exibe métricas de controle e estoque na tela de logística", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.tsx"), "utf8");
    const styles = fs.readFileSync(path.resolve(process.cwd(), "src/styles.css"), "utf8");

    expect(source).toContain("Controle e Estoque");
    expect(source).toContain("Ocupação de caminhão por rota");
    expect(source).toContain("Ruptura de estoque");
    expect(source).toContain("routeOccupancy");
    expect(source).toContain("stockBreaks");
    expect(source).toContain("react-leaflet");
    expect(source).toContain("leaflet/dist/leaflet.css");
    expect(source).toContain("MapContainer");
    expect(source).toContain("TileLayer");
    expect(source).toContain("Marker");
    expect(source).not.toContain("Polyline");
    expect(source).toContain("CircleMarker");
    expect(source).toContain("openstreetmap.org");
    expect(source).toContain("Mapa interativo de entregas");
    expect(source).toContain("deliveryFilterOptions");
    expect(source).toContain("Hoje");
    expect(source).toContain("Próximos 7 dias");
    expect(source).toContain("Próximos 30 dias");
    expect(source).toContain("Entregas atrasadas");
    expect(source).toContain("deliveryMapPoints");
    expect(source).not.toContain("plannedRoutes");
    expect(source).not.toContain("corridor");
    expect(source).toContain("deliveryVolumeRegions");
    expect(source).toContain("Padaria Avenida Marília");
    expect(source).toContain("Supermercado Confiança Bauru");
    expect(source).toContain("Atacado União Ourinhos");
    expect(source).toContain("Status: {delivery.statusLabel}");
    expect(source).toContain("Previsão: {delivery.expectedDelivery}");
    expect(source).toContain("Valor: {formatCurrency(delivery.orderValue)}");
    expect(source).toContain('status === "delayed"');
    expect(source).toContain('status === "attention"');
    expect(source).toContain("#dc2626");
    expect(source).toContain("#f59e0b");
    expect(source).toContain("#16a34a");
    expect(source).toContain("filterDeliveries");
    expect(source).toContain("filteredRoutes");
    expect(source).toContain("routeStatusClassName");
    expect(source).toContain('normalizedStatus === "critico"');
    expect(source).toContain("text-red-600");
    expect(source).toContain('normalizedStatus === "no limite"');
    expect(source).toContain("text-orange-600");
    expect(source).toContain('normalizedStatus === "saudavel"');
    expect(source).toContain("text-blue-600");
    expect(source).toContain('normalizedStatus === "folga"');
    expect(source).toContain("text-green-600");
    expect(source).toContain("getModuleHighlightFromLocation");
    expect(styles).toContain(".delivery-map-marker");
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
    expect(source).toContain("getModuleHighlightFromLocation");
    expect(source).toContain("Indicador destacado pela Torre de Controle");
    expect(source).not.toContain("financeDemoTransactions");
    expect(styles).toContain(".finance-chart-card");
    expect(styles).toContain(".dark .finance-chart-card");
    expect(sidebar).not.toContain('to: "/financas"');
    expect(sidebar).toContain('label: "Finanças"');
  });

<<<<<<< HEAD
  it("cria aba de produtos para visualizar produtos cadastrados", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/produtos.tsx"), "utf8");
    const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");
=======
  it("mescla clientes e finanças com métricas financeiras no topo e lista de clientes abaixo", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(source).toContain("fetchFinanceDashboard");
    expect(source).toContain("Análise Financeira de Clientes");
    expect(source).not.toContain("Análise de Clientes");
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
    expect(sidebar).toContain('label: "Finanças"');
    expect(sidebar).not.toContain('label: "Clientes"');
    expect(sidebar).not.toContain('to: "/financas"');
  });

  it("cria aba de produtos para consultar produtos cadastrados", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/produtos.tsx"), "utf8");
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/produtos")');
    expect(source).toContain("Produtos cadastrados");
<<<<<<< HEAD
    expect(source).toContain("Buscar por SKU ou nome");
    expect(source).toContain("fetchProducts");
    expect(api).toContain("/api/products");
    expect(api).toContain("demoProducts");
    expect(api).toContain("filterDemoProducts");
    expect(sidebar).toContain('to: "/produtos"');
    expect(sidebar).toContain('label: "Produtos"');
  });

  it("exibe médias semanal e mensal na tela de vendas", () => {
=======
    expect(source).toContain("SKU ou descrição do produto");
    expect(source).toContain("fetchProducts");
    expect(source).toContain("useDebouncedValue(search, PRODUCT_SEARCH_DEBOUNCE_MS)");
    expect(source).toContain("PRODUCT_SEARCH_DEBOUNCE_MS = 300");
    expect(source).toContain("new AbortController()");
    expect(source).toContain("Nenhum produto encontrado.");
    expect(source).toContain("Página {page} de {totalPages}");
    expect(source).toContain("getModuleHighlightFromLocation");
    expect(source).toContain("Indicador destacado pela Torre de Controle");
    expect(sidebar).toContain('to: "/produtos"');
    expect(sidebar).toContain('label: "Produtos"');
    expect(sidebar).toContain("PackageSearch");
  });

  it("exibe kpis operacionais de nota fiscal na tela de vendas", () => {
>>>>>>> 63b18f765086c6de4ac2dbaf716dcfa70e776cc1
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const dashboardHelper = fs.readFileSync(path.resolve(process.cwd(), "src/lib/sales-dashboard.ts"), "utf8");

    expect(source).toContain("Notas fiscais");
    expect(source).toContain("Valor das notas");
    expect(source).toContain("Peso movimentado");
    expect(source).toContain("Clientes impactados");
    expect(source).toContain("resolveSalesTimelineGranularity");
    expect(source).toContain("fetchCommercialInvoiceAnalytics");
    expect(source).toContain("AreaChart");
    expect(source).toContain("linearGradient");
    expect(source).toContain("SALES_CHART_CARD_CLASS_NAME");
    expect(source).toContain("getModuleHighlightFromLocation");
    expect(source).toContain("Indicador destacado pela Torre de Controle");
    expect(dashboardHelper).toContain("formatSalesTimelineLabel");
    expect(dashboardHelper).toContain("buildSalesTrendData");
    expect(dashboardHelper).toContain("buildSalesRankingData");
  });

  it("mantém vendas focada em notas fiscais com seletores analíticos", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");

    expect(source).toContain('Label htmlFor="sales-document"');
    expect(source).toContain("Buscar por nota fiscal");
    expect(source).toContain("Notas fiscais");
    expect(source).toContain("Total da nota");
    expect(source).toContain("DialogTitle>Detalhes da Nota Fiscal");
    expect(source).toContain("Itens da nota");
    expect(source).toContain("Carregando detalhes da nota fiscal...");
    expect(source).toContain("fetchCommercialInvoices");
    expect(source).toContain("fetchCommercialInvoiceDetails");
    expect(source).toContain("Evolução das notas fiscais");
    expect(source).toContain("Quantidade de notas");
    expect(source).toContain("Valor total das notas");
    expect(source).toContain("Peso total movimentado");
    expect(source).toContain("Maior quantidade de notas");
    expect(source).toContain("Maior quantidade de itens");
    expect(source).toContain("Maior peso movimentado");
    expect(source).toContain("<CardTitle className=\"text-base text-foreground\">Ranking por empresa</CardTitle>");
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

