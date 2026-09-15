import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("contratos oficiais de custos logísticos", () => {
  const source = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");

  it("expõe custos diário, por rota e o catálogo de pedágios", () => {
    expect(source).toContain("/api/route-costs?");
    expect(source).toContain("/api/routes/${id}/cost");
    expect(source).toContain("/api/logistics/toll-plazas");
    expect(source).toContain("minimumFuelCost");
    expect(source).toContain("maximumFuelCost");
    expect(source).toContain("tollCatalogVersion");
    expect(source).toContain("commercialManualTariffsByAxle");
  });

  it("usa o catálogo do backend no mapa logístico", () => {
    const map = fs.readFileSync(path.resolve(process.cwd(), "src/components/ui/logistics-region-map.tsx"), "utf8");
    expect(map).toContain("fetchTollPlazas()");
    expect(map).toContain("catalog.items");
    expect(map).not.toContain("ALL_TOLL_PLAZAS");
  });
});
