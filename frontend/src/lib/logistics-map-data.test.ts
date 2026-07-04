import { describe, expect, it } from "vitest";
import fs from "node:fs";
import path from "node:path";
import { GRESPAN_HEADQUARTERS, buildCustomerCountByCity, buildDemoLogisticsMapCustomers, buildDemoLogisticsMapRoutes, buildTrafficDelayRanking } from "./logistics-map-data";

describe("logistics map demo fallback", () => {
  it("mantém pelo menos sessenta clientes distribuídos na região atendida", () => {
    const customers = buildDemoLogisticsMapCustomers();
    expect(customers.length).toBeGreaterThanOrEqual(60);
    expect(new Set(customers.map((customer) => customer.city)).size).toBeGreaterThanOrEqual(20);
    expect(customers.map((customer) => customer.city)).toEqual(expect.arrayContaining(["Marília", "Tupã", "Bauru", "Garça", "Pompeia", "Lins", "Lençóis Paulista"]));
    expect(new Set(customers.map((customer) => customer.status))).toEqual(new Set(["Normal", "Atenção", "Crítico"]));
    expect(GRESPAN_HEADQUARTERS.city).toBe("Marília-SP");
  });

  it("gera dados completos e coordenadas distintas para os alfinetes individuais", () => {
    const customers = buildDemoLogisticsMapCustomers();
    expect(customers.every((customer) => customer.name && customer.type && customer.lastDelivery && customer.nextDelivery && customer.route && customer.priority)).toBe(true);
    expect(new Set(customers.map((customer) => `${customer.lat},${customer.lng}`)).size).toBe(customers.length);
    expect(customers.find((customer) => customer.name === "Padaria Santa Clara")).toEqual(expect.objectContaining({ city: "Marília" }));
    expect(customers.find((customer) => customer.name === "Mercado Bom Pão")).toEqual(expect.objectContaining({ city: "Tupã" }));
    expect(customers.find((customer) => customer.name === "Panificadora Avenida")).toEqual(expect.objectContaining({ city: "Bauru" }));
  });

  it("mantém mapa interativo com clientes, trânsito, trajetos tracejados, popup e controles", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/ui/logistics-region-map.tsx"), "utf8");
    const styles = fs.readFileSync(path.resolve(process.cwd(), "src/styles.css"), "utf8");
    expect(source).toContain("openstreetmap.org");
    expect(source).toContain("zoomControl: true");
    expect(source).toContain("scrollWheelZoom: true");
    expect(source).toContain("L.polyline");
    expect(source).toContain('dashArray: "8 8"');
    expect(source).toContain("Trajeto estimado");
    expect(source).toContain("routePopup(route)");
    expect(source).toContain("route.congestionPoints");
    expect(source).not.toContain("cityGroups");
    expect(source).toContain("bindPopup");
    expect(source).toContain("Filtrar por status");
    expect(source).toContain("Filtrar por cidade");
    expect(source).toContain("Filtrar por tipo");
    expect(source).toContain("Centralizar matriz");
    expect(source).toContain("Mostrar clientes");
    expect(source).toContain("Última entrega");
    expect(source).toContain("Próxima entrega");
    expect(source).toContain("Prioridade");
    expect(source).toContain("createCustomerPinIcon");
    expect(source).toContain('logistics-map-pin--${statusClass}');
    expect(source).toContain("L.marker([customer.lat, customer.lng]");
    expect(source).toContain("createCongestionIcon");
    expect(styles).toContain(".logistics-map-pin--normal");
    expect(styles).toContain(".logistics-map-pin--attention");
    expect(styles).toContain(".logistics-map-pin--critical");
    expect(styles).toContain(".logistics-map-congestion--critical");
  });

  it("cria trajetos para todas as cidades e associa congestionamentos às rotas", () => {
    const routes = buildDemoLogisticsMapRoutes();
    const customers = buildDemoLogisticsMapCustomers();
    const coveredCities = new Set(routes.flatMap((route) => route.cities));
    expect(routes).toHaveLength(8);
    expect(customers.every((customer) => coveredCities.has(customer.city))).toBe(true);
    expect(routes.flatMap((route) => route.cities)).toHaveLength(coveredCities.size);
    expect(routes.every((route) => route.path[0].lat === GRESPAN_HEADQUARTERS.lat && route.path[0].lng === GRESPAN_HEADQUARTERS.lng)).toBe(true);
    expect(routes.every((route) => route.congestionPoints.length > 0)).toBe(true);
  });

  it("calcula e ordena atrasos de trânsito conforme Hoje, 7, 30 e 90 dias", () => {
    const routes = buildDemoLogisticsMapRoutes();
    for (const period of [1, 7, 30, 90] as const) {
      const ranking = buildTrafficDelayRanking(routes, period);
      expect(ranking).toHaveLength(routes.length);
      expect(ranking.every((item) => item.delayMinutes >= 0 && item.congestionCount >= 0)).toBe(true);
      expect(ranking.map((item) => item.delayMinutes)).toEqual([...ranking.map((item) => item.delayMinutes)].sort((left, right) => right - left));
    }
    expect(buildTrafficDelayRanking(routes, 90)[0].delayMinutes).toBeGreaterThan(buildTrafficDelayRanking(routes, 1)[0].delayMinutes);
    expect(buildTrafficDelayRanking([], 30)).toEqual([]);
  });

  it("agrupa clientes por cidade e preserva o total ao consolidar as demais", () => {
    const customers = buildDemoLogisticsMapCustomers();
    const ranking = buildCustomerCountByCity(customers, 10);
    expect(ranking[0]).toEqual({ city: "Bauru", customerCount: 8 });
    expect(ranking).toHaveLength(11);
    expect(ranking.at(-1)?.city).toBe("Outras cidades");
    expect(ranking.reduce((total, item) => total + item.customerCount, 0)).toBe(customers.length);
    expect(buildCustomerCountByCity([], 10)).toEqual([]);
  });
});
