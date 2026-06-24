import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("meetings workflow", () => {
  it("mantém a rota de reuniões com layout pai, lista no index e detalhe navegável", () => {
    const layout = read("src/routes/reunioes.tsx");
    const index = read("src/routes/reunioes.index.tsx");
    const detail = read("src/routes/reunioes.$id.tsx");

    expect(layout).toContain("Outlet");
    expect(layout).toContain('createFileRoute("/reunioes")');
    expect(index).toContain('createFileRoute("/reunioes/")');
    expect(index).toContain('to="/reunioes/$id"');
    expect(detail).toContain('createFileRoute("/reunioes/$id")');
  });

  it("mantém a reunião como workflow guiado por etapas persistidas", () => {
    const route = read("src/routes/reunioes.$id.tsx");
    const api = read("src/lib/meetings-api.ts");

    expect(route).toContain('"perguntas_e_respostas"');
    expect(route).toContain("Iniciar");
    expect(route).toContain("Voltar etapa");
    expect(route).toContain("Sugerir problemas");
    expect(route).toContain("Aprovar");
    expect(route).toContain("Gerar análise");
    expect(route).toContain("Histórico");
    expect(api).toContain("generateMeetingAiAnalysis");
    expect(api).toContain("approveMeetingProblem");
    expect(api).toContain("startMeeting");
  });
});
