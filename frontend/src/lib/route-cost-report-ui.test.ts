import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("aba de relatório de custos da logística", () => {
  it("oferece periodicidade e agrupamento do relatório", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.relatorios-custos.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/logistica/relatorios-custos")');
    expect(source).toContain("Diário");
    expect(source).toContain("Semanal");
    expect(source).toContain("Tipo de caminhão");
    expect(source).toContain("buildRouteCostReport");
    expect(source).toContain("fetchRouteRoadPath");
    expect(source).toContain("fetchRoutePathsSequentially");
    expect(source).not.toContain("Promise.allSettled(routes.map");
  });
});
