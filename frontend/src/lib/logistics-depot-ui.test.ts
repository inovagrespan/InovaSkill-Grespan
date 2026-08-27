import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/configuracoes.deposito.tsx"), "utf8");

describe("logistics depot UI", () => {
  it("carrega, valida, salva e testa o OSRM", () => {
    expect(source).toContain("fetchLogisticsDepot");
    expect(source).toContain("validateLogisticsDepotForm");
    expect(source).toContain("updateLogisticsDepot");
    expect(source).toContain("checkOsrmHealth");
    expect(source).toContain("Salvar depósito");
    expect(source).toContain("Testar OSRM");
  });

  it("restringe alteração aos perfis autorizados", () => {
    expect(source).toContain('role === "logistica" || role === "admin" || role === "admin_system"');
    expect(source).toContain("disabled={!canManage || loading}");
  });
});
