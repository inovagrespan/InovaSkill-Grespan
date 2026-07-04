import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("operational, finance and reports pages", () => {
  it("usa o dashboard como painel principal das métricas logísticas", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/dashboard.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/dashboard")');
    expect(source).toContain("LogisticsDashboardMetrics");
    expect(source).not.toContain("Torre de Controle Inteligente");
    expect(source).not.toContain("sortControlTowerCardsByRisk");
  });

  it("exibe dez KPIs mínimos e navegação progressiva em quatro níveis", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.index.tsx"), "utf8");
    const helper = fs.readFileSync(path.resolve(process.cwd(), "src/lib/logistics-dashboard.ts"), "utf8");

    expect(source).toContain("Dashboard logístico");
    expect(source).toContain('title: "Taxa de Devoluções"');
    expect(source).toContain('title: "Taxa de Ocupação"');
    expect(source).toContain('title: "Tempo de Carregamento"');
    expect(source).toContain('title: "Tempo de Trânsito"');
    expect(source).toContain('title: "Custo Logístico Total"');
    expect(source).toContain('title: "Custo Logístico por Rota"');
    expect(source).toContain('title: "Acuracidade de Estoque"');
    expect(source).toContain('title: "Rupturas de Estoque"');
    expect(source).toContain('title: "Índice de Ocorrências"');
    expect(source).toContain('title: "Nível de Atendimento / Fill Rate"');
    expect(source).toContain("formatChange(card.change)");
    expect(source).toContain("statusPresentation(card.status)");
    expect(source).not.toContain("{card.area}");
    expect(source).toContain("relative flex h-full flex-col p-5");
    expect(source).toContain("min-h-11 pr-12");
    expect(source).toContain("flex min-h-10 min-w-0 items-center text-balance");
    expect(source).toContain("absolute right-5 top-5");
    expect(source).not.toContain(">Investigar <");
    expect(source).toContain("DialogContent");
    expect(source).toContain("max-w-4xl");
    expect(source).toContain("Nível 2 · Entender o problema");
    expect(source).toContain("O que aconteceu");
    expect(source).toContain("Por que está neste status");
    expect(source).toContain("Principais fatores que impactaram o resultado");
    expect(source.match(/<DialogContent/g)?.length).toBeGreaterThanOrEqual(2);
    expect(source).not.toContain("SheetContent");
    expect(source).toContain("Nível 3 · Investigação");
    expect(source).toContain("Evidências relacionadas ao problema");
    expect(source).toContain("Nível 4 · Tomada de decisão");
    expect(source).toContain("Ação recomendada");
    expect(source).toContain("buildContextualLogisticsRecommendation");
    expect(source).toContain("<MetricHistoryChart history={metricHistory} periodDays={periodDays}");
    expect(source).toContain("Hoje, por horário");
    expect(source).toContain("Linha temporal: {timelineLabel}");
    expect(source).toContain("logisticsMetricTrendGradient");
    expect(source).toContain("Evolução do indicador");
    expect(source).toContain("buildDemoLogisticsMetricHistory");
    expect(source).toContain("pães congelados e equipamentos de panificação");
    expect(source).toContain("Movimentação de equipamentos alugados");
    expect(source).not.toContain("TabsList");
    expect(source).not.toContain('to="/logistica/mapa"');
    expect(source).not.toContain('to="/logistica/rotas"');
    expect(source).not.toContain("Clientes, rotas e trânsito");
    expect(source).not.toContain("Rotas com mais atrasos por congestionamento");
    expect(source).toContain("Base demonstrativa");
    expect(helper).toContain("calculateLogisticsKpis");
    expect(helper).toContain("filterLogisticsDashboardSource");
    expect(helper).toContain("selectLatestInventoryBySku");
    expect(helper).toContain("compareLogisticsPeriods");
    expect(helper).toContain("buildLogisticsForecast");
  });

  it("move o quadro de rotas para a pagina dedicada de rotas", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");
    const board = fs.readFileSync(path.resolve(process.cwd(), "src/components/logistics-routes-board.tsx"), "utf8");

    expect(route).toContain('createFileRoute("/rotas")');
    expect(route).toContain("<LogisticsRoutesBoard />");
    expect(route).toContain("Rotas");
    expect(board).toContain("Clientes, rotas e trânsito");
    expect(board).toContain("Rotas com mais atrasos por congestionamento");
    expect(board).toContain("LogisticsRegionMap");
    expect(board).toContain("buildTrafficDelayRanking");
  });

  it("cria o mapa principal com as rotas desenhadas", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/mapa.tsx"), "utf8");

    expect(route).toContain('createFileRoute("/mapa")');
    expect(route).toContain("LogisticsRegionMap");
    expect(route).toContain("demoLogisticsMapCustomers");
    expect(route).toContain("demoLogisticsMapRoutes");
    expect(route).toContain("Mapa de rotas");
  });

  it("compacta valores monetarios de logistica em mil e M nos indicadores", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.index.tsx"), "utf8");

    expect(source).toContain('import { formatKpiCompactCurrency } from "@/lib/vendas-formatters";');
    expect(source).toContain("function formatLogisticsCurrency(value: number): string");
    expect(source).toContain("value: formatLogisticsCurrency(metrics.totalLogisticsCost)");
    expect(source).toContain("value: formatLogisticsCurrency(metrics.costPerRoute)");
    expect(source).toContain("formattedValue: formatMetricHistoryValue(metric, point.value)");
    expect(source).not.toContain("currencyFormatter.format");
  });

  it("marca metricas logisticas nao liberadas como em breve sem exibir dados", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.index.tsx"), "utf8");

    expect(source).toContain('const LOGISTICS_METRIC_UNDER_DEVELOPMENT = "em breve";');
    expect(source).toContain('new Set(["occupancy", "stockout"])');
    expect(source).toContain("resolveExecutiveMetricValue(card.id, card.value)");
    expect(source).toContain("const released = isReleasedLogisticsMetric(card.id)");
    expect(source).toContain("onClick={released ? onSelect : undefined}");
    expect(source).toContain("{released && (");
    expect(source).toContain('title: "Taxa de Ocupação", value: formatPercent(metrics.occupancyRatePercent)');
    expect(source).toContain('title: "Rupturas de Estoque", value: `${metrics.stockoutSkuCount} SKUs`');
  });

  it("constrói causas e evidências específicas para a árvore de investigação logística", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.index.tsx"), "utf8");
    const board = fs.readFileSync(path.resolve(process.cwd(), "src/components/logistics-routes-board.tsx"), "utf8");
    const styles = fs.readFileSync(path.resolve(process.cwd(), "src/styles.css"), "utf8");

    expect(source).toContain("buildInvestigationFactors");
    expect(source).toContain('cause: "customer_returns"');
    expect(source).toContain('cause: "low_occupancy"');
    expect(source).toContain('cause: "loading_bottleneck"');
    expect(source).toContain('cause: "congestion"');
    expect(source).toContain('cause: "route_cost"');
    expect(source).toContain('cause: "inventory_divergence"');
    expect(source).toContain('cause: "demand_forecast"');
    expect(source).toContain('cause: "sales_spike"');
    expect(source).toContain('cause: "vehicle_damage"');
    expect(source).toContain("Motorista");
    expect(source).toContain("Veículo");
    expect(source).toContain("Pedido");
    expect(source).toContain("Filial");
    expect(source).toContain("recommendationContext");
    expect(source).toContain("routeOccupancyPresentation");
    expect(source).toContain("No limite");
    expect(source).toContain("Saudável");
    expect(source).toContain("Folga");
    expect(source).toContain("de ocupação");
    expect(source).not.toContain("Distribuição regional dos clientes");
    expect(source).not.toContain("LogisticsRegionMap");
    expect(source).not.toContain("demoLogisticsMapCustomers");
    expect(source).toContain("<AreaChart");
    expect(source).not.toContain("<BarChart ");
    expect(board).toContain("buildTrafficDelayRanking");
    expect(board).toContain("demoLogisticsMapRoutes");
    expect(board).toContain("<LogisticsRegionMap customers={demoLogisticsMapCustomers} routes={demoLogisticsMapRoutes} periodDays={periodDays} compact");
    expect(board).toContain("grid grid-cols-1 gap-4 xl:grid-cols-2");
    expect(board).toContain("Rotas com mais atrasos por congestionamento");
    expect(board).toContain("maxTrafficDelayMinutes");
    expect(board).toContain("INITIAL_TRAFFIC_DELAY_ROUTE_LIMIT = 4");
    expect(board).toContain("visibleTrafficDelayRanking");
    expect(board).toContain("trafficDelayRanking.slice(0, INITIAL_TRAFFIC_DELAY_ROUTE_LIMIT)");
    expect(board).toContain("Exibir mais {hiddenTrafficDelayRouteCount} rotas");
    expect(board).toContain("setShowAllTrafficDelayRoutes(true)");
    expect(board).toContain("trafficSeverityClass(route.severity)");
    expect(board).toContain("formatLogisticsDuration(route.delayMinutes)");
    expect(board).toContain("route.congestionCount} registros");
    expect(styles).toContain(".logistics-map-headquarters");
    expect(styles).toContain(".logistics-map-popup");
    expect(styles).toContain(".logistics-city-chart-card");
    expect(styles).toContain(".dark .logistics-city-chart-card");
    expect(styles).toContain(".logistics-modal-chart");
    expect(styles).toContain(".dark .logistics-modal-chart");
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
    expect(sidebar).not.toContain('label: "Finanças"');
  });

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
    expect(source).toContain("onClick={() => openDetails(item.ClienteId ?? item.customerCode ?? item.clienteId)}");
    expect(sidebar).not.toContain('to: "/clientes"');
    expect(sidebar).not.toContain('label: "Finanças"');
    expect(sidebar).not.toContain('label: "Clientes"');
    expect(sidebar).not.toContain('to: "/financas"');
  });

  it("mantem a pagina de produtos fora da estrutura da sidebar", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/produtos.tsx"), "utf8");
    const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/produtos")');
    expect(source).toContain("Produtos cadastrados");
    expect(source).toContain("Buscar por SKU ou nome");
    expect(source).toContain("fetchProducts");
    expect(api).toContain("/api/products");
    expect(api).toContain("demoProducts");
    expect(api).toContain("filterDemoProducts");
    expect(sidebar).not.toContain('to: "/produtos"');
    expect(sidebar).not.toContain('label: "Produtos"');
  });

  it("exibe kpis operacionais de nota fiscal na tela de vendas", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const salesTower = fs.readFileSync(path.resolve(process.cwd(), "src/components/SalesControlTower.tsx"), "utf8");
    const dashboardHelper = fs.readFileSync(path.resolve(process.cwd(), "src/lib/sales-dashboard.ts"), "utf8");

    expect(source).toContain('createFileRoute("/vendas")({ component: SalesControlTower })');
    expect(salesTower).toContain("Torre Comercial");
    expect(source).toContain("Notas fiscais");
    expect(source).toContain("Valor das notas");
    expect(source).toContain("Peso movimentado");
    expect(source).toContain("Clientes impactados");
    expect(source).toContain("resolveSalesTimelineGranularity");
    expect(source).toContain("fetchCommercialInvoiceAnalytics");
    expect(source).toContain("AreaChart");
    expect(source).toContain("linearGradient");
    expect(source).toContain("SALES_CHART_CARD_CLASS_NAME");
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
    expect(source).toContain("<TableHead className=\"text-right\">Fat. 12M</TableHead>");
    expect(source).toContain("DialogTitle>Detalhes do Cliente");
    expect(source).toContain("loadCustomerDetails");
  });

  it("remove a aba de RH da navegação e do dashboard", () => {
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
    const dashboard = fs.readFileSync(path.resolve(process.cwd(), "src/routes/dashboard.tsx"), "utf8");

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
