import { describe, expect, it } from "vitest";
import { canRoleAccessPath } from "@/lib/access-control";
import fs from "node:fs";
import path from "node:path";

describe("acesso ao consumo de IA", () => {
  it.each(["admin", "admin_system"])("permite o perfil %s", (role) => {
    expect(canRoleAccessPath(role, "/administracao/consumo-ia")).toBe(true);
  });

  it.each(["diretor", "vendas", "logistica"])("bloqueia o perfil %s", (role) => {
    expect(canRoleAccessPath(role, "/administracao/consumo-ia")).toBe(false);
  });

  it("exibe métricas de tempo, referências de redução e a amostra auditável", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/administracao.consumo-ia.tsx"), "utf8");
    expect(source).toContain("Tempo médio");
    expect(source).toContain("Mediana");
    expect(source).toContain("P95");
    expect(source).toContain("Redução vs. 10 min");
    expect(source).toContain("Redução vs. 2 h");
    expect(source).toContain("Amostra das consultas");
    expect(source).toContain("questionReceivedAt");
    expect(source).toContain("Resposta concluída");
    expect(source).toContain("responseCompletedAt");
  });
});
