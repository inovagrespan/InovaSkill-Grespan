import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { canRoleAccessPath } from "@/lib/access-control";

describe("tela administrativa de usuários", () => {
  it("fica disponível somente para admin_system", () => {
    expect(canRoleAccessPath("admin_system", "/administracao/usuarios")).toBe(true);
    expect(canRoleAccessPath("admin", "/administracao/usuarios")).toBe(false);
    expect(canRoleAccessPath("diretor", "/administracao/usuarios")).toBe(false);
  });

  it("oferece os perfis funcionais e valida as senhas", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/administracao.usuarios.tsx"), "utf8");
    for (const role of ["diretor", "vendas", "logistica", "admin", "admin_system"]) expect(source).toContain(`value: "${role}"`);
    expect(source).toContain("form.password.length < 6");
    expect(source).toContain("form.password !== form.confirmPassword");
    expect(source).toContain("Usuário cadastrado com sucesso.");
  });
});
