import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("grupos condensados da sidebar", () => {
  const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");

  it.each([
    ["WhatsApp", ["/meu-whatsapp", "/simulador-whatsapp", "/administracao/whatsapp"]],
    ["Logística", ["/logistica/rotas", "/veiculos/tipos", "/configuracoes/deposito", "/mapa", "/producao"]],
    ["Cadastros", ["/clientes", "/notas-fiscais", "/produtos", "/estoque"]],
    ["Administração", ["/importacoes/files", "/processamentos", "/administracao/usuarios"]],
  ])("reúne os atalhos de %s", (label, paths) => {
    expect(source).toContain(`label: "${label}"`);
    for (const itemPath of paths) expect(source).toContain(`"${itemPath}"`);
  });

  it("mantém o dashboard fora das sanfonas", () => {
    const groupsSource = source.slice(source.indexOf("const menuGroups"), source.indexOf("export function getVisibleSidebarItemsForRole"));
    expect(groupsSource).not.toContain('"/dashboard"');
  });
});
