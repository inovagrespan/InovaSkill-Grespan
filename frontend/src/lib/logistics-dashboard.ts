export const LOGISTICS_PERIOD_OPTIONS = [1, 7, 30, 90] as const;

export type LogisticsPeriodDays = (typeof LOGISTICS_PERIOD_OPTIONS)[number];

export type LogisticsRouteRecord = {
  date: string;
  routeId: string;
  routeName: string;
  vehicleType: string;
  loadedKg: number;
  capacityKg: number;
  loadingMinutes: number;
  transitMinutes: number;
  logisticsCost: number;
  requestedUnits: number;
  dispatchedUnits: number;
  deliveredUnits: number;
  returnedUnits: number;
  damagedUnits: number;
  stops: number;
};

export type LogisticsInventoryRecord = {
  date: string;
  sku: string;
  productName: string;
  warehouse: string;
  systemStock: number;
  countedStock: number;
  demandUnits: number;
  availableUnits: number;
};

export type LogisticsDashboardSource = {
  routes: LogisticsRouteRecord[];
  inventory: LogisticsInventoryRecord[];
};

export type LogisticsKpis = {
  returnRatePercent: number;
  occupancyRatePercent: number;
  averageLoadingMinutes: number;
  averageTransitMinutes: number;
  totalLogisticsCost: number;
  costPerRoute: number;
  inventoryAccuracyPercent: number;
  stockoutSkuCount: number;
  damageRatePercent: number;
  fillRatePercent: number;
  routeCount: number;
};

export type LogisticsKpiComparison = {
  current: LogisticsKpis;
  previous: LogisticsKpis;
  changes: {
    occupancyRatePercent: number | null;
    stockoutSkuCount: number | null;
    averageTransitMinutes: number | null;
    damageRatePercent: number | null;
    averageLoadingMinutes: number | null;
    costPerRoute: number | null;
    inventoryAccuracyPercent: number | null;
    fillRatePercent: number | null;
    returnRatePercent: number | null;
    totalLogisticsCost: number | null;
  };
};

export type LogisticsForecast = {
  horizonDays: LogisticsPeriodDays;
  possibleStockouts: number;
  projectedLogisticsCost: number;
  occurrenceGrowthPercent: number;
  delayRiskPercent: number;
};

export type LogisticsRootCause =
  | "demand_forecast"
  | "supplier_delay"
  | "sales_spike"
  | "congestion"
  | "vehicle_damage"
  | "customer_returns"
  | "low_occupancy"
  | "loading_bottleneck"
  | "inventory_divergence"
  | "route_cost";

export type LogisticsRecommendationContext = {
  subject: string;
  route?: string;
  customer?: string;
  product?: string;
  vehicle?: string;
};

export type LogisticsMetricHistoryKey =
  | "returns"
  | "occupancy"
  | "loading"
  | "transit"
  | "total-cost"
  | "route-cost"
  | "inventory-accuracy"
  | "stockout"
  | "occurrences"
  | "fill-rate";

export type LogisticsMetricHistoryPoint = { label: string; value: number };

export type LogisticsRouteSummary = {
  routeId: string;
  routeName: string;
  vehicleType: string;
  occupancyPercent: number;
  loadedKg: number;
  capacityKg: number;
  logisticsCost: number;
  stops: number;
};

export type LogisticsRoutePerformancePoint = {
  routeId: string;
  routeName: string;
  occupancyPercent: number;
  averageTransitMinutes: number;
  tripCount: number;
};

const PERCENT_SCALE = 100;
const PERCENT_DECIMAL_PLACES = 1;
const MONEY_DECIMAL_PLACES = 2;
const MILLISECONDS_PER_DAY = 86_400_000;
const FORECAST_BASE_PERIOD_DAYS = 30;
const TRANSIT_RISK_REFERENCE_MINUTES = 240;

function nonNegative(value: number): number {
  return Number.isFinite(value) ? Math.max(0, value) : 0;
}

function round(value: number, decimalPlaces: number): number {
  const factor = 10 ** decimalPlaces;
  return Math.round((value + Number.EPSILON) * factor) / factor;
}

function safePercent(numerator: number, denominator: number): number {
  if (denominator <= 0) return 0;
  return round(Math.min(PERCENT_SCALE, (numerator / denominator) * PERCENT_SCALE), PERCENT_DECIMAL_PLACES);
}

function percentageChange(current: number, previous: number): number | null {
  if (previous === 0) return current === 0 ? 0 : null;
  return round(((current - previous) / Math.abs(previous)) * PERCENT_SCALE, PERCENT_DECIMAL_PLACES);
}

function startOfPeriod(referenceDate: string, periodDays: LogisticsPeriodDays): number {
  const referenceTime = Date.parse(`${referenceDate}T00:00:00Z`);
  return referenceTime - (periodDays - 1) * MILLISECONDS_PER_DAY;
}

function isInsidePeriod(date: string, periodStart: number, periodEnd: number): boolean {
  const value = Date.parse(`${date}T00:00:00Z`);
  return Number.isFinite(value) && value >= periodStart && value <= periodEnd;
}

export function filterLogisticsDashboardSource(
  source: LogisticsDashboardSource,
  periodDays: LogisticsPeriodDays,
  referenceDate: string,
): LogisticsDashboardSource {
  const periodStart = startOfPeriod(referenceDate, periodDays);
  const periodEnd = Date.parse(`${referenceDate}T23:59:59.999Z`);

  return {
    routes: source.routes.filter((record) => isInsidePeriod(record.date, periodStart, periodEnd)),
    inventory: source.inventory.filter((record) => isInsidePeriod(record.date, periodStart, periodEnd)),
  };
}

export function selectLatestInventoryBySku(records: LogisticsInventoryRecord[]): LogisticsInventoryRecord[] {
  const latestBySku = new Map<string, LogisticsInventoryRecord>();
  for (const record of records) {
    const current = latestBySku.get(record.sku);
    if (!current || record.date > current.date) latestBySku.set(record.sku, record);
  }
  return [...latestBySku.values()].sort((left, right) => left.sku.localeCompare(right.sku));
}

export function calculateLogisticsKpis(source: LogisticsDashboardSource): LogisticsKpis {
  const routes = source.routes;
  const inventory = selectLatestInventoryBySku(source.inventory);
  const routeIds = new Set(routes.map((route) => route.routeId));

  const totals = routes.reduce(
    (result, route) => ({
      loadedKg: result.loadedKg + nonNegative(route.loadedKg),
      capacityKg: result.capacityKg + nonNegative(route.capacityKg),
      loadingMinutes: result.loadingMinutes + nonNegative(route.loadingMinutes),
      transitMinutes: result.transitMinutes + nonNegative(route.transitMinutes),
      cost: result.cost + nonNegative(route.logisticsCost),
      requestedUnits: result.requestedUnits + nonNegative(route.requestedUnits),
      dispatchedUnits: result.dispatchedUnits + nonNegative(route.dispatchedUnits),
      deliveredUnits: result.deliveredUnits + nonNegative(route.deliveredUnits),
      returnedUnits: result.returnedUnits + nonNegative(route.returnedUnits),
      damagedUnits: result.damagedUnits + nonNegative(route.damagedUnits),
    }),
    {
      loadedKg: 0,
      capacityKg: 0,
      loadingMinutes: 0,
      transitMinutes: 0,
      cost: 0,
      requestedUnits: 0,
      dispatchedUnits: 0,
      deliveredUnits: 0,
      returnedUnits: 0,
      damagedUnits: 0,
    },
  );

  const systemStock = inventory.reduce((total, item) => total + nonNegative(item.systemStock), 0);
  const inventoryDifference = inventory.reduce(
    (total, item) => total + Math.abs(nonNegative(item.systemStock) - nonNegative(item.countedStock)),
    0,
  );
  const inventoryAccuracyPercent = systemStock > 0
    ? safePercent(Math.max(0, systemStock - inventoryDifference), systemStock)
    : inventoryDifference === 0 ? PERCENT_SCALE : 0;

  return {
    returnRatePercent: safePercent(totals.returnedUnits, totals.dispatchedUnits),
    occupancyRatePercent: safePercent(totals.loadedKg, totals.capacityKg),
    averageLoadingMinutes: routes.length ? round(totals.loadingMinutes / routes.length, PERCENT_DECIMAL_PLACES) : 0,
    averageTransitMinutes: routes.length ? round(totals.transitMinutes / routes.length, PERCENT_DECIMAL_PLACES) : 0,
    totalLogisticsCost: round(totals.cost, MONEY_DECIMAL_PLACES),
    costPerRoute: routeIds.size ? round(totals.cost / routeIds.size, MONEY_DECIMAL_PLACES) : 0,
    inventoryAccuracyPercent,
    stockoutSkuCount: inventory.filter((item) => nonNegative(item.availableUnits) < nonNegative(item.demandUnits)).length,
    damageRatePercent: safePercent(totals.damagedUnits, totals.dispatchedUnits),
    fillRatePercent: safePercent(totals.deliveredUnits, totals.requestedUnits),
    routeCount: routeIds.size,
  };
}

export function compareLogisticsPeriods(
  source: LogisticsDashboardSource,
  periodDays: LogisticsPeriodDays,
  referenceDate: string,
): LogisticsKpiComparison {
  const currentSource = filterLogisticsDashboardSource(source, periodDays, referenceDate);
  const previousReferenceTime = Date.parse(`${referenceDate}T00:00:00Z`) - periodDays * MILLISECONDS_PER_DAY;
  const previousReferenceDate = new Date(previousReferenceTime).toISOString().slice(0, 10);
  const current = calculateLogisticsKpis(currentSource);
  const previous = calculateLogisticsKpis(filterLogisticsDashboardSource(source, periodDays, previousReferenceDate));

  return {
    current,
    previous,
    changes: {
      occupancyRatePercent: percentageChange(current.occupancyRatePercent, previous.occupancyRatePercent),
      stockoutSkuCount: percentageChange(current.stockoutSkuCount, previous.stockoutSkuCount),
      averageTransitMinutes: percentageChange(current.averageTransitMinutes, previous.averageTransitMinutes),
      damageRatePercent: percentageChange(current.damageRatePercent, previous.damageRatePercent),
      averageLoadingMinutes: percentageChange(current.averageLoadingMinutes, previous.averageLoadingMinutes),
      costPerRoute: percentageChange(current.costPerRoute, previous.costPerRoute),
      inventoryAccuracyPercent: percentageChange(current.inventoryAccuracyPercent, previous.inventoryAccuracyPercent),
      fillRatePercent: percentageChange(current.fillRatePercent, previous.fillRatePercent),
      returnRatePercent: percentageChange(current.returnRatePercent, previous.returnRatePercent),
      totalLogisticsCost: percentageChange(current.totalLogisticsCost, previous.totalLogisticsCost),
    },
  };
}

export function buildLogisticsForecast(
  source: LogisticsDashboardSource,
  horizonDays: LogisticsPeriodDays,
  referenceDate: string,
): LogisticsForecast {
  const comparison = compareLogisticsPeriods(source, 30, referenceDate);
  const horizonFactor = horizonDays / FORECAST_BASE_PERIOD_DAYS;
  const positiveStockoutTrend = Math.max(0, comparison.changes.stockoutSkuCount ?? 0) / PERCENT_SCALE;
  const latestInventoryCount = selectLatestInventoryBySku(
    filterLogisticsDashboardSource(source, 90, referenceDate).inventory,
  ).length;
  const possibleStockouts = Math.min(
    latestInventoryCount,
    Math.max(0, Math.ceil(comparison.current.stockoutSkuCount * (1 + positiveStockoutTrend * horizonFactor))),
  );
  const costTrend = Math.max(-PERCENT_SCALE, comparison.changes.totalLogisticsCost ?? 0) / PERCENT_SCALE;
  const projectedLogisticsCost = round(
    Math.max(0, (comparison.current.totalLogisticsCost / FORECAST_BASE_PERIOD_DAYS) * horizonDays * (1 + costTrend * horizonFactor)),
    MONEY_DECIMAL_PLACES,
  );
  const occurrenceGrowthPercent = round(
    Math.max(0, comparison.changes.damageRatePercent ?? 0) * horizonFactor,
    PERCENT_DECIMAL_PLACES,
  );
  const transitBaselineRisk = safePercent(comparison.current.averageTransitMinutes, TRANSIT_RISK_REFERENCE_MINUTES);
  const transitTrendRisk = Math.max(0, comparison.changes.averageTransitMinutes ?? 0) * horizonFactor;
  const delayRiskPercent = round(Math.min(PERCENT_SCALE, transitBaselineRisk * 0.7 + transitTrendRisk * 0.3), PERCENT_DECIMAL_PLACES);

  return { horizonDays, possibleStockouts, projectedLogisticsCost, occurrenceGrowthPercent, delayRiskPercent };
}

export function buildContextualLogisticsRecommendation(
  cause: LogisticsRootCause,
  context: LogisticsRecommendationContext,
): string {
  if (cause === "demand_forecast") return `Recalibrar a previsão de demanda de ${context.product ?? context.subject} usando a venda recente e revisar o estoque de segurança antes do próximo ciclo.`;
  if (cause === "supplier_delay") return `Antecipar a compra de ${context.product ?? context.subject} e renegociar o prazo do fornecedor responsável para proteger os pedidos pendentes.`;
  if (cause === "sales_spike") return `Elevar temporariamente o estoque de segurança de ${context.product ?? context.subject} e monitorar diariamente a continuidade do pico de vendas.`;
  if (cause === "congestion") return `Replanejar a rota ${context.route ?? context.subject} para uma janela de menor tráfego e comparar o Transit Time nas próximas três viagens.`;
  if (cause === "vehicle_damage") return `Realizar inspeção preventiva no veículo ${context.vehicle ?? context.subject}, com foco em suspensão, baú e controle de temperatura antes da próxima carga.`;
  if (cause === "customer_returns") return `Abrir auditoria comercial e logística para ${context.customer ?? context.subject}, validando pedido, separação e motivo das devoluções recorrentes.`;
  if (cause === "low_occupancy") return `Consolidar os pedidos da rota ${context.route ?? context.subject} ou substituir o veículo por um de menor capacidade na próxima programação.`;
  if (cause === "loading_bottleneck") return `Pré-separar a carga de ${context.subject} e reposicionar a equipe para eliminar a espera observada antes da próxima janela de expedição.`;
  if (cause === "inventory_divergence") return `Executar inventário cíclico imediato de ${context.product ?? context.subject} e bloquear ajustes manuais até reconciliar o saldo físico e sistêmico.`;
  return `Revisar a composição de custo da rota ${context.route ?? context.subject}, priorizando ocupação, pedágios e reentregas antes de renegociar a operação.`;
}

const DEMO_METRIC_HISTORY_VALUES: Record<LogisticsMetricHistoryKey, number[]> = {
  returns: [2.9, 2.7, 2.5, 2.6, 2.2, 2.0, 1.8],
  occupancy: [71, 74, 77, 76, 81, 84, 86],
  loading: [64, 61, 58, 59, 55, 52, 49],
  transit: [258, 249, 243, 246, 232, 222, 214],
  "total-cost": [48_600, 50_200, 49_700, 52_900, 51_800, 53_600, 54_400],
  "route-cost": [4_050, 4_180, 4_140, 4_410, 4_320, 4_470, 4_530],
  "inventory-accuracy": [93.8, 94.6, 95.1, 95.0, 96.2, 97.0, 97.6],
  stockout: [9, 8, 7, 7, 5, 4, 3],
  occurrences: [3.8, 3.4, 3.1, 3.3, 2.7, 2.3, 1.9],
  "fill-rate": [88.5, 89.7, 91.2, 90.8, 93.1, 94.4, 95.6],
};

function historyLabels(periodDays: LogisticsPeriodDays): string[] {
  if (periodDays === 1) return ["06h", "09h", "12h", "15h", "18h", "21h", "24h"];
  const pointCount = 7;
  const referenceTime = Date.parse(`${LOGISTICS_REFERENCE_DATE}T00:00:00Z`);
  const startTime = referenceTime - (periodDays - 1) * MILLISECONDS_PER_DAY;
  return Array.from({ length: pointCount }, (_, index) => {
    const progress = index / (pointCount - 1);
    const dayOffset = Math.round((periodDays - 1) * progress);
    const pointTime = startTime + dayOffset * MILLISECONDS_PER_DAY;
    const isoDate = new Date(pointTime).toISOString().slice(0, 10);
    const [year, month, day] = isoDate.split("-");
    void year;
    return `${day}/${month}`;
  });
}

export function buildDemoLogisticsMetricHistory(
  metric: LogisticsMetricHistoryKey,
  periodDays: LogisticsPeriodDays,
): LogisticsMetricHistoryPoint[] {
  const labels = historyLabels(periodDays);
  return DEMO_METRIC_HISTORY_VALUES[metric].map((value, index) => ({ label: labels[index], value }));
}

export function summarizeLogisticsRoutes(records: LogisticsRouteRecord[]): LogisticsRouteSummary[] {
  const summaries = new Map<string, LogisticsRouteSummary>();
  for (const record of records) {
    const current = summaries.get(record.routeId) ?? {
      routeId: record.routeId,
      routeName: record.routeName,
      vehicleType: record.vehicleType,
      occupancyPercent: 0,
      loadedKg: 0,
      capacityKg: 0,
      logisticsCost: 0,
      stops: 0,
    };
    current.loadedKg += nonNegative(record.loadedKg);
    current.capacityKg += nonNegative(record.capacityKg);
    current.logisticsCost += nonNegative(record.logisticsCost);
    current.stops += nonNegative(record.stops);
    current.occupancyPercent = safePercent(current.loadedKg, current.capacityKg);
    summaries.set(record.routeId, current);
  }
  return [...summaries.values()].sort((left, right) => right.occupancyPercent - left.occupancyPercent);
}

export function buildLogisticsRoutePerformance(records: LogisticsRouteRecord[]): LogisticsRoutePerformancePoint[] {
  const grouped = new Map<string, {
    routeName: string;
    loadedKg: number;
    capacityKg: number;
    transitMinutes: number;
    tripCount: number;
  }>();

  for (const record of records) {
    const current = grouped.get(record.routeId) ?? {
      routeName: record.routeName,
      loadedKg: 0,
      capacityKg: 0,
      transitMinutes: 0,
      tripCount: 0,
    };
    current.loadedKg += nonNegative(record.loadedKg);
    current.capacityKg += nonNegative(record.capacityKg);
    current.transitMinutes += nonNegative(record.transitMinutes);
    current.tripCount += 1;
    grouped.set(record.routeId, current);
  }

  return [...grouped.entries()]
    .map(([routeId, route]) => ({
      routeId,
      routeName: route.routeName,
      occupancyPercent: safePercent(route.loadedKg, route.capacityKg),
      averageTransitMinutes: route.tripCount
        ? round(route.transitMinutes / route.tripCount, PERCENT_DECIMAL_PLACES)
        : 0,
      tripCount: route.tripCount,
    }))
    .sort((left, right) => right.occupancyPercent - left.occupancyPercent || left.routeName.localeCompare(right.routeName));
}

export function formatLogisticsDuration(totalMinutes: number): string {
  const minutes = Math.round(nonNegative(totalMinutes));
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  return remainingMinutes ? `${hours}h ${remainingMinutes}min` : `${hours}h`;
}

export const LOGISTICS_REFERENCE_DATE = "2026-06-24";

export const demoLogisticsDashboardSource: LogisticsDashboardSource = {
  routes: [
    { date: "2026-06-24", routeId: "ROT-02", routeName: "São Paulo → ABC", vehicleType: "Toco", loadedKg: 6280, capacityKg: 7300, loadingMinutes: 46, transitMinutes: 172, logisticsCost: 1890, requestedUnits: 530, dispatchedUnits: 516, deliveredUnits: 501, returnedUnits: 7, damagedUnits: 3, stops: 15 },
    { date: "2026-06-23", routeId: "ROT-01", routeName: "Campinas → Interior SP", vehicleType: "Truck 3/4", loadedKg: 7820, capacityKg: 8500, loadingMinutes: 52, transitMinutes: 225, logisticsCost: 2380, requestedUnits: 640, dispatchedUnits: 620, deliveredUnits: 602, returnedUnits: 12, damagedUnits: 5, stops: 18 },
    { date: "2026-06-22", routeId: "ROT-02", routeName: "São Paulo → ABC", vehicleType: "Toco", loadedKg: 6140, capacityKg: 7300, loadingMinutes: 44, transitMinutes: 168, logisticsCost: 1840, requestedUnits: 510, dispatchedUnits: 498, deliveredUnits: 486, returnedUnits: 8, damagedUnits: 3, stops: 14 },
    { date: "2026-06-21", routeId: "ROT-03", routeName: "Ribeirão Preto → Norte", vehicleType: "Carreta", loadedKg: 18900, capacityKg: 25000, loadingMinutes: 68, transitMinutes: 310, logisticsCost: 4210, requestedUnits: 880, dispatchedUnits: 852, deliveredUnits: 826, returnedUnits: 14, damagedUnits: 7, stops: 22 },
    { date: "2026-06-20", routeId: "ROT-04", routeName: "Sorocaba → Oeste", vehicleType: "Truck", loadedKg: 10450, capacityKg: 11000, loadingMinutes: 57, transitMinutes: 246, logisticsCost: 2970, requestedUnits: 720, dispatchedUnits: 701, deliveredUnits: 684, returnedUnits: 11, damagedUnits: 6, stops: 16 },
    { date: "2026-06-10", routeId: "ROT-01", routeName: "Campinas → Interior SP", vehicleType: "Truck 3/4", loadedKg: 7460, capacityKg: 8500, loadingMinutes: 49, transitMinutes: 218, logisticsCost: 2290, requestedUnits: 610, dispatchedUnits: 592, deliveredUnits: 578, returnedUnits: 9, damagedUnits: 4, stops: 17 },
    { date: "2026-05-12", routeId: "ROT-02", routeName: "São Paulo → ABC", vehicleType: "Toco", loadedKg: 5980, capacityKg: 7300, loadingMinutes: 46, transitMinutes: 175, logisticsCost: 1790, requestedUnits: 490, dispatchedUnits: 475, deliveredUnits: 461, returnedUnits: 7, damagedUnits: 4, stops: 13 },
  ],
  inventory: [
    { date: "2026-06-24", sku: "PAN-104", productName: "Pão Francês Congelado 60g", warehouse: "CD Central", systemStock: 318, countedStock: 310, demandUnits: 560, availableUnits: 310 },
    { date: "2026-06-24", sku: "PAN-221", productName: "Pão de Queijo Congelado 1kg", warehouse: "CD Campinas", systemStock: 138, countedStock: 134, demandUnits: 250, availableUnits: 134 },
    { date: "2026-06-24", sku: "PAN-318", productName: "Croissant Congelado 80g", warehouse: "CD Ribeirão", systemStock: 88, countedStock: 86, demandUnits: 78, availableUnits: 86 },
    { date: "2026-06-23", sku: "PAN-104", productName: "Pão Francês Congelado 60g", warehouse: "CD Central", systemStock: 332, countedStock: 320, demandUnits: 580, availableUnits: 320 },
    { date: "2026-06-23", sku: "PAN-221", productName: "Pão de Queijo Congelado 1kg", warehouse: "CD Campinas", systemStock: 145, countedStock: 140, demandUnits: 260, availableUnits: 140 },
    { date: "2026-06-23", sku: "PAN-318", productName: "Croissant Congelado 80g", warehouse: "CD Ribeirão", systemStock: 94, countedStock: 90, demandUnits: 80, availableUnits: 90 },
    { date: "2026-06-08", sku: "PAN-104", productName: "Pão Francês Congelado 60g", warehouse: "CD Central", systemStock: 420, countedStock: 414, demandUnits: 390, availableUnits: 414 },
    { date: "2026-05-10", sku: "PAN-221", productName: "Pão de Queijo Congelado 1kg", warehouse: "CD Campinas", systemStock: 310, countedStock: 302, demandUnits: 290, availableUnits: 302 },
  ],
};
