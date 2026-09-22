import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("mapa rodoviário no detalhe da rota", () => {
  it("consulta a geometria OSRM e desenha o percurso no Leaflet", () => {
    const api = readSource("src/lib/importer-api.ts");
    const map = readSource("src/components/RouteRoadMap.tsx");

    expect(api).toContain("/road-path");
    expect(map).toContain("L.polyline(geometry");
    expect(map).toContain('dashArray: "10 8"');
    expect(map).toContain("[longitude, latitude]");
    expect(map).toContain("Percurso calculado sobre a malha rodoviária");
  });

  it("mantém o componente desacoplado da consulta HTTP", () => {
    const map = readSource("src/components/RouteRoadMap.tsx");
    expect(map).not.toContain("fetch(");
    expect(map).toContain("route.geometry");
  });

  it("exibe o mapa ao abrir uma rota real", () => {
    const routePage = readSource("src/routes/rotas.tsx");

    expect(routePage).toContain('import { RouteRoadMap } from "@/components/RouteRoadMap";');
    expect(routePage).toContain("fetchRouteRoadPath(route.id)");
    expect(routePage).toContain("setRoadPathError(error.message)");
    expect(routePage).toContain("Mapa da rota");
    expect(routePage).toContain("<RouteRoadMap route={roadPath} />");
  });

  it("exibe os KPIs de pedágio, combustível e gasto total no detalhe", () => {
    const routePage = readSource("src/routes/rotas.tsx");

    expect(routePage).toContain("fetchRouteCost(route.id)");
    expect(routePage).not.toContain("estimateRouteTollCost");
    expect(routePage).toContain("ConsolidatedRouteTollKpi");
    const tollKpi = readSource("src/components/ConsolidatedRouteTollKpi.tsx");
    expect(tollKpi).toContain("Gasto estimado com pedágio");
    expect(tollKpi).toContain("Praças e tarifas automáticas usadas na consolidação oficial");
    expect(routePage).toContain("font-semibold uppercase tracking-wider text-primary");
    expect(routePage).toContain("lg:grid-cols-3");
    expect(routePage).toContain("sm:grid-cols-2 lg:col-span-2");
    expect(routePage).toContain("route-kpi-grid");
    expect(routePage).toContain("lg:h-full lg:grid-rows-2");
    expect(routePage).toContain("route-kpi-card");
    expect(tollKpi).toContain("route-kpi-card");
    expect(routePage).toContain("route-kpi-card border-primary/30 bg-primary/5");
    expect(routePage).toContain("lg:aspect-square");
    expect(routePage).toContain("Gastos totais");
    expect(routePage).toContain("Combustível + pedágio");
  });

  it("exibe os clientes vinculados no detalhe da rota original", () => {
    const api = readSource("src/lib/importer-api.ts");
    const routePage = readSource("src/routes/rotas.tsx");

    expect(api).toContain("customers:");
    expect(api).toContain("customerName?: string | null");
    expect(routePage).toContain("Clientes da rota");
    expect(routePage).toContain("formatCustomerAddress(customer.address)");
    expect(routePage).toContain("customer.name");
  });

  it("mantém o detalhe largo e impede overflow horizontal dos cards", () => {
    const routePage = readSource("src/routes/rotas.tsx");
    const styles = readSource("src/styles.css");

    expect(routePage).toContain("w-[96vw] max-w-6xl overflow-x-hidden");
    expect(routePage).toContain("grid min-w-0 grid-cols-1 gap-3 text-sm lg:grid-cols-3");
    expect(styles).toContain(".route-kpi-card.aspect-square");
    expect(styles).toContain("aspect-ratio: auto");
  });
});
