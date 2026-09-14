import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { isApplicationPath } from "./access-control";

describe("página não encontrada", () => {
  const root = fs.readFileSync(path.resolve(process.cwd(), "src/routes/__root.tsx"), "utf8");
  const page = fs.readFileSync(
    path.resolve(process.cwd(), "src/components/NotFoundPage.tsx"),
    "utf8",
  );

  it("distingue rotas da aplicação de endereços inexistentes", () => {
    expect(isApplicationPath("/dashboard")).toBe(true);
    expect(isApplicationPath("/logistica/rotas/rota-123")).toBe(true);
    expect(isApplicationPath("/pagina-inexistente")).toBe(false);
  });

  it("usa o fallback customizado sem ignorar o controle de acesso das rotas válidas", () => {
    expect(root).toContain("notFoundComponent: NotFoundPage");
    expect(root).toContain(
      "isApplicationPath(location.pathname) && !canRoleAccessPath(role, location.pathname)",
    );
  });

  it("oferece contexto e caminhos de recuperação acessíveis", () => {
    expect(page).toContain("Parece que você saiu da rota.");
    expect(page).toContain("router.history.back()");
    expect(page).toContain('<Link to="/dashboard">');
    expect(page).toContain("Voltar");
    expect(page).toContain("Ir para o painel");
    expect(page).not.toContain("AAI Seguri");
  });

  it("mantém a composição responsiva e alinhada à marca", () => {
    expect(page).toContain("<BrandLogo");
    expect(page).toContain("min-h-dvh");
    expect(page).toContain("sm:flex-row");
    expect(page).toContain("text-primary");
  });
});
