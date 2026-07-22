import { describe, expect, it } from "vitest";
import {
  calculateLogisticsKpis,
  buildLogisticsForecast,
  buildContextualLogisticsRecommendation,
  buildLogisticsRoutePerformance,
  buildDemoLogisticsMetricHistory,
  compareLogisticsPeriods,
  filterLogisticsDashboardSource,
  formatLogisticsDuration,
  selectLatestInventoryBySku,
  summarizeLogisticsRoutes,
  type LogisticsDashboardSource,
} from "./logistics-dashboard";

const source: LogisticsDashboardSource = {
  routes: [
    { date: "2026-06-23", routeId: "A", routeName: "Rota A", vehicleType: "Truck", loadedKg: 80, capacityKg: 100, loadingMinutes: 30, transitMinutes: 120, logisticsCost: 300, requestedUnits: 100, dispatchedUnits: 90, deliveredUnits: 85, returnedUnits: 5, damagedUnits: 2, stops: 2 },
    { date: "2026-06-22", routeId: "B", routeName: "Rota B", vehicleType: "Toco", loadedKg: 60, capacityKg: 100, loadingMinutes: 50, transitMinutes: 180, logisticsCost: 500, requestedUnits: 100, dispatchedUnits: 100, deliveredUnits: 95, returnedUnits: 5, damagedUnits: 3, stops: 3 },
    { date: "2026-05-01", routeId: "A", routeName: "Rota A", vehicleType: "Truck", loadedKg: 50, capacityKg: 100, loadingMinutes: 20, transitMinutes: 60, logisticsCost: 200, requestedUnits: 50, dispatchedUnits: 50, deliveredUnits: 50, returnedUnits: 0, damagedUnits: 0, stops: 1 },
  ],
  inventory: [
    { date: "2026-06-20", sku: "X", productName: "Produto X", warehouse: "CD", systemStock: 100, countedStock: 95, demandUnits: 110, availableUnits: 95 },
    { date: "2026-06-23", sku: "X", productName: "Produto X", warehouse: "CD", systemStock: 100, countedStock: 98, demandUnits: 90, availableUnits: 98 },
    { date: "2026-06-23", sku: "Y", productName: "Produto Y", warehouse: "CD", systemStock: 50, countedStock: 47, demandUnits: 60, availableUnits: 47 },
  ],
};

describe("logistics dashboard metrics", () => {
  it("calcula fórmula, agrupamento, arredondamento e consistência entre custo total e custo por rota", () => {
    const filtered = filterLogisticsDashboardSource(source, 7, "2026-06-23");
    const metrics = calculateLogisticsKpis(filtered);

    expect(metrics).toEqual({
      returnRatePercent: 5.3,
      occupancyRatePercent: 70,
      averageLoadingMinutes: 40,
      averageTransitMinutes: 150,
      totalLogisticsCost: 800,
      costPerRoute: 400,
      inventoryAccuracyPercent: 96.7,
      stockoutSkuCount: 1,
      damageRatePercent: 2.6,
      fillRatePercent: 90,
      routeCount: 2,
    });
    expect(metrics.costPerRoute * metrics.routeCount).toBe(metrics.totalLogisticsCost);
  });

  it("aplica período inclusivo e usa somente a posição mais recente de cada SKU", () => {
    const filtered = filterLogisticsDashboardSource(source, 30, "2026-06-23");
    expect(filtered.routes).toHaveLength(2);
    expect(selectLatestInventoryBySku(filtered.inventory)).toEqual([
      expect.objectContaining({ sku: "X", date: "2026-06-23" }),
      expect.objectContaining({ sku: "Y", date: "2026-06-23" }),
    ]);
  });

  it("trata base vazia, divisões por zero, valores inválidos e contagens não negativas", () => {
    const empty = calculateLogisticsKpis({ routes: [], inventory: [] });
    expect(empty).toEqual({
      returnRatePercent: 0,
      occupancyRatePercent: 0,
      averageLoadingMinutes: 0,
      averageTransitMinutes: 0,
      totalLogisticsCost: 0,
      costPerRoute: 0,
      inventoryAccuracyPercent: 100,
      stockoutSkuCount: 0,
      damageRatePercent: 0,
      fillRatePercent: 0,
      routeCount: 0,
    });

    const invalid = calculateLogisticsKpis({
      routes: [{ ...source.routes[0], loadedKg: -20, capacityKg: 0, logisticsCost: Number.NaN, deliveredUnits: 200 }],
      inventory: [{ ...source.inventory[0], systemStock: 0, countedStock: 10, availableUnits: -1, demandUnits: 1 }],
    });
    expect(invalid.occupancyRatePercent).toBe(0);
    expect(invalid.totalLogisticsCost).toBe(0);
    expect(invalid.fillRatePercent).toBe(100);
    expect(invalid.inventoryAccuracyPercent).toBe(0);
    expect(invalid.stockoutSkuCount).toBe(1);
  });

  it("consolida múltiplas viagens da mesma rota sem duplicar a rota no divisor de custo", () => {
    const summaries = summarizeLogisticsRoutes(source.routes);
    const routeA = summaries.find((route) => route.routeId === "A");
    expect(routeA).toEqual(expect.objectContaining({ loadedKg: 130, capacityKg: 200, occupancyPercent: 65, logisticsCost: 500, stops: 3 }));
    expect(calculateLogisticsKpis(source).routeCount).toBe(2);
  });

  it("formata tempos abaixo e acima de uma hora", () => {
    expect(formatLogisticsDuration(42.4)).toBe("42 min");
    expect(formatLogisticsDuration(150)).toBe("2h 30min");
    expect(formatLogisticsDuration(180)).toBe("3h");
    expect(formatLogisticsDuration(-5)).toBe("0 min");
  });

  it("compara o período atual com a janela anterior sem misturar as datas", () => {
    const comparisonSource: LogisticsDashboardSource = {
      routes: [
        { ...source.routes[0], date: "2026-06-23", loadedKg: 80, transitMinutes: 150, damagedUnits: 4 },
        { ...source.routes[0], date: "2026-06-16", loadedKg: 50, transitMinutes: 100, damagedUnits: 2 },
      ],
      inventory: [
        { ...source.inventory[0], date: "2026-06-23", availableUnits: 80, demandUnits: 100 },
        { ...source.inventory[0], date: "2026-06-16", availableUnits: 120, demandUnits: 100 },
      ],
    };

    const comparison = compareLogisticsPeriods(comparisonSource, 7, "2026-06-23");
    expect(comparison.current.occupancyRatePercent).toBe(80);
    expect(comparison.previous.occupancyRatePercent).toBe(50);
    expect(comparison.changes.occupancyRatePercent).toBe(60);
    expect(comparison.changes.averageTransitMinutes).toBe(50);
    expect(comparison.current.stockoutSkuCount).toBe(1);
    expect(comparison.previous.stockoutSkuCount).toBe(0);
    expect(comparison.changes.stockoutSkuCount).toBeNull();
  });

  it("projeta ruptura, custo, ocorrências e atraso com limites válidos", () => {
    const forecast = buildLogisticsForecast(source, 30, "2026-06-23");
    expect(forecast.horizonDays).toBe(30);
    expect(forecast.possibleStockouts).toBeGreaterThanOrEqual(0);
    expect(forecast.possibleStockouts).toBeLessThanOrEqual(2);
    expect(forecast.projectedLogisticsCost).toBeGreaterThanOrEqual(0);
    expect(forecast.occurrenceGrowthPercent).toBeGreaterThanOrEqual(0);
    expect(forecast.delayRiskPercent).toBeGreaterThanOrEqual(0);
    expect(forecast.delayRiskPercent).toBeLessThanOrEqual(100);
  });

  it("mantém previsões zeradas e risco limitado quando não existe base", () => {
    const forecast = buildLogisticsForecast({ routes: [], inventory: [] }, 90, "2026-06-23");
    expect(forecast).toEqual({
      horizonDays: 90,
      possibleStockouts: 0,
      projectedLogisticsCost: 0,
      occurrenceGrowthPercent: 0,
      delayRiskPercent: 0,
    });
  });

  it("gera recomendações específicas conforme causa e contexto investigado", () => {
    expect(buildContextualLogisticsRecommendation("demand_forecast", { subject: "ruptura", product: "Pão Francês 60g" }))
      .toContain("Pão Francês 60g");
    expect(buildContextualLogisticsRecommendation("congestion", { subject: "atraso", route: "Campinas → Interior" }))
      .toContain("Campinas → Interior");
    expect(buildContextualLogisticsRecommendation("vehicle_damage", { subject: "avaria", vehicle: "Truck 3/4 - 07" }))
      .toContain("Truck 3/4 - 07");
    expect(buildContextualLogisticsRecommendation("customer_returns", { subject: "devolução", customer: "Padaria Avenida" }))
      .toContain("Padaria Avenida");
    expect(buildContextualLogisticsRecommendation("demand_forecast", { subject: "ruptura", product: "Pão Francês 60g" }))
      .not.toBe(buildContextualLogisticsRecommendation("supplier_delay", { subject: "ruptura", product: "Pão Francês 60g" }));
  });

  it("agrupa ocupação e Transit Time por rota para os gráficos operacionais", () => {
    const performance = buildLogisticsRoutePerformance(source.routes);

    expect(performance).toEqual([
      { routeId: "B", routeName: "Rota B", occupancyPercent: 60, averageTransitMinutes: 180, tripCount: 1 },
      { routeId: "A", routeName: "Rota A", occupancyPercent: 65, averageTransitMinutes: 90, tripCount: 2 },
    ].sort((left, right) => right.occupancyPercent - left.occupancyPercent));
    expect(performance.reduce((total, route) => total + route.tripCount, 0)).toBe(source.routes.length);
    expect(buildLogisticsRoutePerformance([])).toEqual([]);
  });

  it("fornece histórico específico, completo e coerente para cada KPI logístico", () => {
    const metricKeys = ["returns", "occupancy", "loading", "transit", "total-cost", "route-cost", "inventory-accuracy", "stockout", "occurrences", "fill-rate"] as const;
    const histories = metricKeys.map((metric) => buildDemoLogisticsMetricHistory(metric, 30));
    expect(histories.every((history) => history.length === 7 && history.every((point) => point.value > 0))).toBe(true);
    expect(new Set(histories.map((history) => history.map((point) => point.value).join(","))).size).toBe(metricKeys.length);
    expect(buildDemoLogisticsMetricHistory("occupancy", 30).at(-1)?.value).toBe(86);
    expect(buildDemoLogisticsMetricHistory("stockout", 30).at(-1)?.value).toBe(3);
    expect(buildDemoLogisticsMetricHistory("fill-rate", 30).at(-1)?.value).toBe(95.6);
    expect(buildDemoLogisticsMetricHistory("transit", 1).map((point) => point.label)).toEqual(["06h", "09h", "12h", "15h", "18h", "21h", "24h"]);
    expect(buildDemoLogisticsMetricHistory("occupancy", 7).map((point) => point.label)).toEqual(["18/06", "19/06", "20/06", "21/06", "22/06", "23/06", "24/06"]);
    expect(buildDemoLogisticsMetricHistory("returns", 30).map((point) => point.label)).toEqual(["26/05", "31/05", "05/06", "10/06", "14/06", "19/06", "24/06"]);
    expect(buildDemoLogisticsMetricHistory("total-cost", 90).map((point) => point.label)).toEqual(["27/03", "11/04", "26/04", "11/05", "25/05", "09/06", "24/06"]);
  });

  it("mantém uma base demonstrativa coerente com congelados e equipamentos alugados", async () => {
    const { demoLogisticsDashboardSource } = await import("./logistics-dashboard");
    const productNames = demoLogisticsDashboardSource.inventory.map((item) => item.productName);
    const rentals = demoLogisticsDashboardSource.equipmentRentals ?? [];

    expect(productNames).toEqual(expect.arrayContaining([
      expect.stringContaining("Pão Francês Congelado"),
      expect.stringContaining("Pão de Queijo Congelado"),
      expect.stringContaining("Croissant Congelado"),
      expect.stringContaining("Coxinha de Frango Congelada"),
      expect.stringContaining("Esfiha de Carne Congelada"),
    ]));
    expect(rentals.map((item) => item.equipmentType)).toEqual(expect.arrayContaining(["Forno", "Freezer"]));
    expect(rentals.every((item) => item.monthlyRentalAmount > 0 && item.serviceRouteId.length > 0)).toBe(true);
    expect(rentals.reduce((total, item) => total + item.monthlyRentalAmount, 0)).toBe(4_270);
  });
});
