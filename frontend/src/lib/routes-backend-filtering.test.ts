import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("filtros de rotas", () => {
  it("envia busca, data e criticidade ao backend sem filtrar a página localmente", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");
    const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

    expect(route).toContain("fetchImportedRoutes(p, pageSize");
    expect(route).not.toContain("routes.filter(");
    expect(api).toContain('params.set("search", filters.search)');
    expect(api).toContain('params.set("date", filters.date)');
    expect(api).toContain('params.set("occupancyLevel", filters.occupancyLevel)');
  });

  it("exibe apenas informações operacionais da rota", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");

    expect(route).not.toContain("Arquivo:");
    expect(route).not.toContain("Importado:");
    expect(route).not.toContain(">Importadas<");
    expect(route).toContain("cidade(s)");
    expect(route).toContain("entrega(s)");
  });
});
