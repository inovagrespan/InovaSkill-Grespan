import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("vínculos de clientes e rotas", () => {
  it("remove rotas da listagem e as apresenta no detalhe com inclusão manual", () => {
    const list = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const detail = fs.readFileSync(path.resolve(process.cwd(), "src/components/CustomerConsumptionDialog.tsx"), "utf8");
    const api = fs.readFileSync(path.resolve(process.cwd(), "src/lib/importer-api.ts"), "utf8");
    expect(list).not.toContain("routeAssignments.map");
    expect(list).not.toContain(">Rotas</TableHead>");
    expect(detail).toContain("Rotas do cliente");
    expect(detail).toContain("Sem rota");
    expect(detail).toContain("routeAssignments.map");
    expect(detail).toContain("Adicionar à rota");
    expect(api).toContain("addCustomerRouteAssignment");
    expect(api).toContain("/route-assignments");
  });

  it("usa busca reativa compartilhada na revisão assistida", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/importacoes.files.tsx"), "utf8");
    expect(source).toContain("AssignmentCandidateResolver");
    expect(source).toContain("TEXT_SEARCH_DEBOUNCE_MS");
    expect(source).toContain("fetchImportErrorCandidates");
  });
});
