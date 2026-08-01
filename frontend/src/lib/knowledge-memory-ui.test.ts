import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";

describe("painel de memórias da IA", () => {
  it("restringe a navegação a administradores e oferece revisão e desativação", () => {
    const access = readFileSync("src/lib/access-control.ts", "utf8");
    const page = readFileSync("src/routes/administracao.memorias.tsx", "utf8");
    expect(access).toContain('{ path: "/administracao/memorias", roles: ADMIN_ROLES }');
    expect(page).toContain("Memórias da IA");
    expect(page).toContain("deleteKnowledgeMemory");
    expect(page).toContain("Privada");
  });
});
