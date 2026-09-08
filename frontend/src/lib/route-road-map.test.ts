import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("mapa rodoviário no detalhe da rota", () => {
  it("consulta a geometria OSRM e desenha o percurso no Leaflet", () => {
    const api = readSource("src/lib/importer-api.ts");
    const route = readSource("src/routes/rotas.tsx");
    const map = readSource("src/components/RouteRoadMap.tsx");

    expect(api).toContain("/road-path");
    expect(route).toContain("fetchRouteRoadPath");
    expect(route).toContain("Traçado pelas ruas e rodovias");
    expect(map).toContain("L.polyline(geometry");
    expect(map).toContain('dashArray: "10 8"');
    expect(map).toContain("[longitude, latitude]");
    expect(map).toContain("Percurso calculado sobre a malha rodoviária");
  });

  it("mantém os detalhes visíveis quando o OSRM falha", () => {
    const route = readSource("src/routes/rotas.tsx");

    expect(route).toContain("setRoadPathError(error.message)");
    expect(route).toContain("setSelectedRoute(detail)");
  });
});
