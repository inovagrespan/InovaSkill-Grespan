import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readSidebar(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
}

describe("sidebar navigation", () => {
  it("remove submenu unico de clientes e mantem acesso principal direto", () => {
    const source = readSidebar();

    expect(source).toContain('to: "/clientes"');
    expect(source).toContain('label: "Clientes"');
    expect(source).not.toContain('to: "/clientes/analise-comercial", label: "Análise Comercial", icon: Activity');
  });

  it("mantem importacoes como item unico sem submenu files", () => {
    const source = readSidebar();

    expect(source).toContain('to: "/importacoes", label: "Importações", icon: FileUp');
    expect(source).not.toContain('to: "/importacoes/files", label: "Files", icon: FileUp');
    expect(source).not.toContain("item.children?.map");
  });
});
