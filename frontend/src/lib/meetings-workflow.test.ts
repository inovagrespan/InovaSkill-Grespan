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

  it("mantem o layout de reunioes estavel na visualizacao web", () => {
    const index = read("src/routes/reunioes.index.tsx");
    const detail = read("src/routes/reunioes.$id.tsx");

    expect(detail).toContain("MEETING_DETAIL_GRID_CLASS_NAME");
    expect(detail).toContain("xl:grid-cols-[minmax(0,1fr)_minmax(280px,320px)]");
    expect(detail).toContain("MEETING_STAGE_TRACK_CLASS_NAME");
    expect(detail).toContain("flex min-w-max items-center gap-1 p-3");
    expect(detail).toContain("min-w-0 space-y-4");
    expect(detail).toContain("min-w-0 overflow-hidden rounded-xl");
    expect(detail).toContain("grid grid-cols-1 gap-4 lg:grid-cols-3");

    expect(index).toContain("grid min-w-0 grid-cols-1 gap-3");
    expect(index).toContain("sm:grid-cols-[minmax(0,1fr)_auto]");
    expect(index).toContain("flex min-w-0 flex-wrap items-center gap-3");
  });
});
