import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { canRoleAccessPath } from "./access-control";

describe("assistant full page", () => {
  const root = fs.readFileSync(path.resolve(process.cwd(), "src/routes/__root.tsx"), "utf8");
  const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/assistente.tsx"), "utf8");
  const component = fs.readFileSync(
    path.resolve(process.cwd(), "src/components/BusinessAssistant.tsx"),
    "utf8",
  );

  it("permite a aba de chat para todos os perfis funcionais", () => {
    for (const role of ["diretor", "vendas", "logistica", "admin", "admin_system"]) {
      expect(canRoleAccessPath(role, "/assistente")).toBe(true);
    }
  });

  it("renderiza o chat como página de altura total", () => {
    expect(route).toContain('createFileRoute("/assistente")');
    expect(route).toContain('<BusinessAssistant variant="page" />');
    expect(route).toContain("h-dvh");
    expect(component).toContain('variant?: "floating" | "page"');
    expect(component).toContain('? "h-full min-h-0 w-full"');
    expect(route).not.toContain("p-3 pt-16");
  });

  it("remove o assistente flutuante quando a página dedicada está ativa", () => {
    expect(root).toContain('const isAssistantPage = pathname === "/assistente"');
    expect(root).toContain("canRenderPrivateApp && !isAssistantPage");
  });
});
