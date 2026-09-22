import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { canRoleAccessPath } from "./access-control";

describe("localizações simuladas", () => {
  const route = fs.readFileSync(
    path.resolve(process.cwd(), "src/routes/administracao.localizacoes-simuladas.tsx"),
    "utf8",
  );

  it("restringe a tela ao administrador do sistema", () => {
    expect(canRoleAccessPath("admin_system", "/administracao/localizacoes-simuladas")).toBe(true);
    for (const role of ["admin", "diretor", "vendas", "logistica"])
      expect(canRoleAccessPath(role, "/administracao/localizacoes-simuladas")).toBe(false);
  });

  it("oferece preenchimento único, auditoria e reversão com aviso operacional", () => {
    expect(route).toContain("Preencher coordenadas ausentes");
    expect(route).toContain("Reverter execução");
    expect(route).toContain("Preencher sem recalcular");
    expect(route).toContain("Preencher e recalcular");
    expect(route).toContain("runCoordinateSimulation(recalculateDependents)");
    expect(route).toContain("pontos são substitutos usados para validar os cálculos");
    expect(route).toContain("TEXT_SEARCH_DEBOUNCE_MS");
  });
});
