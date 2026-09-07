import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("grupo de inteligência artificial na sidebar", () => {
  const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

  it("agrupa as funcionalidades relacionadas à IA em uma sanfona acessível", () => {
    expect(source).toContain('"/assistente"');
    expect(source).toContain('"/administracao/consumo-ia"');
    expect(source).toContain('"/administracao/memorias"');
    expect(source).toContain("Inteligência Artificial");
    expect(source).toContain("aria-expanded={groupIsOpen}");
    expect(source).toContain('key: "ai"');
  });

  it("mantém acesso direto quando o perfil só enxerga um item de IA", () => {
    expect(source).toContain("visibleGroupItems.length === 1 || showCollapsed");
    expect(source).toContain("group.paths.some");
  });
});
