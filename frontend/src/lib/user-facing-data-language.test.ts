import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("linguagem de dados apresentada ao usuário", () => {
  it("não exibe ressalvas de dados demonstrativos ou fictícios", () => {
    const userFacingSources = [
      "src/routes/logistica.index.tsx",
      "src/routes/relatorios.tsx",
      "src/routes/simulacao.tsx",
      "src/components/RouteVehicleSimulationDialog.tsx",
      "src/components/BusinessAssistant.tsx",
      "src/lib/importer-api.ts",
    ].map(read).join("\n");

    for (const disclaimer of [
      "Dados reais quando disponíveis",
      "Base demonstrativa complementar",
      "Dado fictício",
      "dados fictícios",
      "Dados de demonstração",
      "Arquivo demo",
      "linha demo",
      "base demo",
      "Cenário simulado",
      "Demanda simulada",
    ]) {
      expect(userFacingSources).not.toContain(disclaimer);
    }
  });
});
