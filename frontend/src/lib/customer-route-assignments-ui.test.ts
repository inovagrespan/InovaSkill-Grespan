import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("vínculos de clientes e rotas", () => {
  it("exibe todas as rotas com dia e ausência explícita", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    expect(source).toContain("item.routeAssignments.map");
    expect(source).toContain("assignment.weekday");
    expect(source).toContain("assignment.routeName");
    expect(source).toContain('item.routeAssignments.length === 0 ? "—"');
  });

  it("usa busca reativa compartilhada na revisão assistida", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/importacoes.files.tsx"), "utf8");
    expect(source).toContain("AssignmentCandidateResolver");
    expect(source).toContain("TEXT_SEARCH_DEBOUNCE_MS");
    expect(source).toContain("fetchImportErrorCandidates");
  });
});
