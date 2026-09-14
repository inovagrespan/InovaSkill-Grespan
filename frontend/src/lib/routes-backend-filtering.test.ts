import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("filtros de rotas", () => {
  it("envia busca, data, dia e criticidade ao backend sem filtrar a página localmente", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");
    const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

    expect(route).toContain("fetchImportedRoutes(p, pageSize");
    expect(route).not.toContain("routes.filter(");
    expect(api).toContain('params.set("search", filters.search)');
    expect(api).toContain('params.set("date", filters.date)');
    expect(api).toContain('params.set("weekday", filters.weekday)');
    expect(api).toContain('params.set("occupancyLevel", filters.occupancyLevel)');
    expect(route).toContain('aria-label="Filtrar por dia da semana"');
    expect(route).toContain("weekday: weekday === ALL_WEEKDAYS ? undefined : weekday");
    expect(route).toContain("<SelectItem value={ALL_WEEKDAYS}>Todos os dias</SelectItem>");
  });

  it("exibe apenas informações operacionais da rota", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");
    expect(route).not.toContain(">Importadas<");
    expect(route).not.toContain("Arquivo:");
    expect(route).not.toContain("Importado:");
    expect(route).toContain("cidade(s)");
    expect(route).toContain("entrega(s)");
    expect(route).toContain("Saída {r.departureTime.slice(0, 5)}");
    const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");
    expect(api).toContain("departureTime: string | null");
  });
});
