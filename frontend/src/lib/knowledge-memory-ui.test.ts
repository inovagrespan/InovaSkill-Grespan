import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { formatKnowledgeMemoryUpdatedAt, getKnowledgeMemoryOwnerOptions, getKnowledgeMemoryScopeLabel } from "./knowledge-memory-ui";

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
    expect(page).toContain("Salvar");
    expect(page).toContain("Reativar memória");
    expect(page).toContain("Atualizada {formatKnowledgeMemoryUpdatedAt(memory.updatedAt)}");
    expect(page).toContain('<UserRound className="size-3.5" />{memory.createdByUserName}');
    expect(page).not.toContain("Registrada por");
    expect(page).toContain("md:grid-cols-2");
    expect(page).toContain('aria-label="Filtrar por usuário"');
    expect(page).toContain("useDebouncedValue(search, TEXT_SEARCH_DEBOUNCE_MS)");
  });

  it("monta opções de usuário sem duplicatas, ordenadas e sem memórias empresariais", () => {
    const base = { id: "1", scope: "user" as const, createdByUserId: 1, createdByUserName: "Admin", subject: "Tema", content: "Conteúdo", isActive: true, createdAt: "2026-08-01", updatedAt: "2026-08-01", supersedesMemoryId: null };
    const options = getKnowledgeMemoryOwnerOptions([
      { ...base, ownerUserId: 2, ownerUserName: "Zélia" },
      { ...base, id: "2", ownerUserId: 1, ownerUserName: "Ana" },
      { ...base, id: "3", ownerUserId: 2, ownerUserName: "Nome anterior" },
      { ...base, id: "4", scope: "company", ownerUserId: null, ownerUserName: null },
    ]);

    expect(options).toEqual([{ id: 1, name: "Ana" }, { id: 2, name: "Nome anterior" }]);
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
