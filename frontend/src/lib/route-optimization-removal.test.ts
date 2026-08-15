import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSource(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("remoção da roteirização legada", () => {
  it("mantém a rota logística como compatibilidade e redireciona para a tela de rotas", () => {
    const route = readSource("src/routes/logistica.rotas.tsx");

    expect(route).toContain('createFileRoute("/logistica/rotas")');
    expect(route).toContain('<Navigate to="/rotas" replace />');
  });

  it("não mantém contratos nem chamadas ao endpoint antigo de otimização", () => {
    const api = readSource("src/lib/importer-api.ts");

    expect(api).not.toContain("RouteOptimizationRun");
    expect(api).not.toContain("route-optimization-runs");
    expect(api).not.toContain("latest-optimization");
  });
});
