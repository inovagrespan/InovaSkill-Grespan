import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("aba de relatório de custos da logística", () => {
  it("consome a consolidação oficial e compara os cenários do dia", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/logistica.relatorios-custos.tsx"), "utf8");

    expect(source).toContain('createFileRoute("/logistica/relatorios-custos")');
    expect(source).toContain("fetchDailyRouteCosts");
    expect(source).toContain("Rotas atuais");
    expect(source).toContain("Cenário otimizado");
    expect(source).toContain("Diário");
    expect(source).toContain("Semanal");
    expect(source).toContain("Tipo de caminhão");
    expect(source).toContain("groupRouteCostItems");
    expect(source).toContain("não representa substituição 1:1 de uma rota");
    expect(source).toContain("unavailableItemCount");
    expect(source).not.toContain("estimateRouteTollCost");
    expect(source).not.toContain("buildRouteCostReport");
  });
});
