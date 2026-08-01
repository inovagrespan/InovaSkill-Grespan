import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { formatKnowledgeMemoryUpdatedAt, getKnowledgeMemoryScopeLabel } from "./knowledge-memory-ui";

describe("painel de memórias da IA", () => {
  it("restringe a navegação a administradores e oferece revisão e desativação", () => {
    const access = readFileSync("src/lib/access-control.ts", "utf8");
    const page = readFileSync("src/routes/administracao.memorias.tsx", "utf8");
    const presentation = readFileSync("src/lib/knowledge-memory-ui.ts", "utf8");
    expect(access).toContain('{ path: "/administracao/memorias", roles: ADMIN_ROLES }');
    expect(page).toContain("Memórias da IA");
    expect(page).toContain("deleteKnowledgeMemory");
    expect(presentation).toContain("Privada");
  });

  it("organiza as memórias em cards com contexto, conteúdo e ações identificados", () => {
    const page = readFileSync("src/routes/administracao.memorias.tsx", "utf8");

    expect(page).toContain('aria-label="Filtros de memórias"');
    expect(page).toContain('aria-label="Lista de memórias"');
    expect(page).toContain("Conteúdo lembrado");
    expect(page).toContain("Salvar alterações");
    expect(page).toContain("Reativar memória");
    expect(page).toContain("Atualizada {formatKnowledgeMemoryUpdatedAt(memory.updatedAt)}");
  });

  it("apresenta corretamente memórias empresariais e privadas", () => {
    expect(getKnowledgeMemoryScopeLabel({ scope: "company", ownerUserName: null })).toBe("Empresa");
    expect(getKnowledgeMemoryScopeLabel({ scope: "user", ownerUserName: "Leonardo" })).toBe("Privada · Leonardo");
    expect(getKnowledgeMemoryScopeLabel({ scope: "user", ownerUserName: "  " })).toBe("Privada · Usuário");
  });

  it("trata datas inválidas sem exibir texto técnico", () => {
    expect(formatKnowledgeMemoryUpdatedAt("data-inválida")).toBe("em data não informada");
    expect(formatKnowledgeMemoryUpdatedAt("2026-08-01T14:30:00-03:00")).not.toContain("Invalid");
  });
});
