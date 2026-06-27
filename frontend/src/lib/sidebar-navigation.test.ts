import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { getVisibleSidebarItemsForRole } from "@/components/AppSidebar";

function readSidebar(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
}

describe("sidebar navigation", () => {
  it("exibe o menu principal por perfil e libera tudo para diretor", () => {
    const diretorItems = getVisibleSidebarItemsForRole("Diretor").map((item) => item.label);
    const vendasItems = getVisibleSidebarItemsForRole("vendas").map((item) => item.label);

    expect(diretorItems).toEqual([
      "Dashboard",
      "Alertas",
      "Vendas",
      "Logística",
      "Produção",
      "Administrativo",
      "Reuniões",
    ]);
    expect(vendasItems).toEqual(["Dashboard", "Alertas", "Vendas"]);
  });

  it("mantem a ordem visual solicitada no menu lateral", () => {
    const source = readSidebar();

    expect(source).toContain('to: "/dashboard"');
    expect(source).toContain('label: "Dashboard"');
    expect(source).toContain('to: "/alertas"');
    expect(source).toContain('label: "Alertas"');
    expect(source).toContain('to: "/vendas"');
    expect(source).toContain('label: "Vendas"');
    expect(source).toContain('to: "/logistica"');
    expect(source).toContain('label: "Logística"');
    expect(source).toContain('to: "/produtos"');
    expect(source).toContain('label: "Produção"');
    expect(source).toContain('to: "/processamentos"');
    expect(source).toContain('label: "Processamento"');
    expect(source).toContain('to: "/administrativo"');
    expect(source).toContain('label: "Administrativo"');
    expect(source).not.toContain('to: "/relatorios"');
    expect(source).toContain('diretor');
    expect(source).not.toContain('label: "Clientes"');
    expect(source).not.toContain('to: "/clientes/analise-comercial", label: "Análise Comercial", icon: Activity');
  });

  it("remove itens fora do novo desenho do menu", () => {
    const source = readSidebar();

    expect(source).not.toContain('to: "/importacoes", label: "Importações", icon: FileUp');
    expect(source).not.toContain('to: "/importacoes/files", label: "Files", icon: FileUp');
    expect(source).not.toContain('label: "Finanças"');
    expect(source).not.toContain('label: "Relatórios"');
    expect(source).not.toContain('label: "Pendências"');
    expect(source).not.toContain('label: "Simulação"');
    expect(source).not.toContain("item.children?.map");
  });

  it("mantem controles da sidebar acessiveis quando recolhida sem carregar a lista de notificacoes", () => {
    const source = readSidebar();

    expect(source).toContain("Expandir sidebar");
    expect(source).toContain("AnimatedMenuIcon");
    expect(source).toContain("open ? \"translate-y-0 rotate-45\"");
    expect(source).toContain("open ? \"translate-y-0 -rotate-45\"");
    expect(source).toContain("aria-expanded={!collapsed}");
    expect(source).toContain("flex size-10 shrink-0 items-center justify-center rounded-full");
    expect(source).not.toContain("collapsed && \"hidden\"");
    expect(source).toContain('collapsed ? "md:w-[76px]"');
    expect(source).toContain('collapsed ? "size-10 p-0"');
    expect(source).not.toContain("fetchNotifications");
    expect(source).not.toContain("Central de atenção");
  });

  it("usa Conecta360 como marca principal depois do login", () => {
    const source = readSidebar();

    expect(source).toContain('import { BrandLogo } from "@/components/BrandLogo";');
    expect(source).toContain('aria-label="Conecta360"');
    expect(source).toContain("renderBrandHeader");
  });
});
