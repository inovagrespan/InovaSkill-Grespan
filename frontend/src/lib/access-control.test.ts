import { describe, expect, it } from "vitest";
import { canRoleAccessPath, getDefaultPathForRole } from "./access-control";

describe("access control", () => {
  it.each([
    ["diretor", "/rotas", true],
    ["diretor", "/importacoes/files", false],
    ["diretor", "/processamentos", false],
    ["vendas", "/clientes", true],
    ["vendas", "/notas-fiscais", true],
    ["vendas", "/rotas", true],
    ["vendas", "/producao", false],
    ["logistica", "/rotas", true],
    ["logistica", "/veiculos/tipos", true],
    ["logistica", "/producao", true],
    ["logistica", "/importacoes/files", false],
    ["admin", "/processamentos", true],
    ["admin_system", "/importacoes/files", true],
  ])("%s acessando %s retorna %s", (role, path, expected) => {
    expect(canRoleAccessPath(role, path)).toBe(expected);
  });

  it("aplica a mesma regra às subrotas", () => {
    expect(canRoleAccessPath("vendas", "/detections/123")).toBe(false);
    expect(canRoleAccessPath("vendas", "/notificacoes")).toBe(false);
    expect(canRoleAccessPath("vendas", "/logistica/rotas")).toBe(false);
  });

  it("bloqueia perfis genéricos, ausentes e caminhos desconhecidos", () => {
    expect(canRoleAccessPath("gestor", "/dashboard")).toBe(false);
    expect(canRoleAccessPath(null, "/dashboard")).toBe(false);
    expect(canRoleAccessPath("admin", "/area-inexistente")).toBe(false);
    expect(getDefaultPathForRole("gestor")).toBe("/login");
  });
});
