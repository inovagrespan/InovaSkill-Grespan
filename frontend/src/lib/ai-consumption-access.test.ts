import { describe, expect, it } from "vitest";
import { canRoleAccessPath } from "@/lib/access-control";

describe("acesso ao consumo de IA", () => {
  it.each(["admin", "admin_system"])("permite o perfil %s", (role) => {
    expect(canRoleAccessPath(role, "/administracao/consumo-ia")).toBe(true);
  });

  it.each(["diretor", "vendas", "logistica"])("bloqueia o perfil %s", (role) => {
    expect(canRoleAccessPath(role, "/administracao/consumo-ia")).toBe(false);
  });
});
