import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { getVisibleSidebarItemsForRole } from "@/components/AppSidebar";

function readSidebar(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
}

describe("sidebar navigation", () => {
  it("exibe o menu principal por perfil e libera importacoes para diretor e administradores", () => {
    const diretorItems = getVisibleSidebarItemsForRole("Diretor").map((item) => item.label);
    const vendasItems = getVisibleSidebarItemsForRole("vendas").map((item) => item.label);
    const adminItems = getVisibleSidebarItemsForRole("admin").map((item) => item.label);
    const adminSystemItems = getVisibleSidebarItemsForRole("admin_system").map((item) => item.label);

    expect(diretorItems).toEqual([
      "Dashboard",
      "Rotas",
      "Tipos de Veículo",
      "Mapa",
      "Clientes",
      "Notas Fiscais",
      "Produtos",
      "Estoque",
      "Produção",
      "Importações",
      "Detecção",
    ]);
    expect(vendasItems).toEqual(["Dashboard", "Rotas", "Tipos de Veículo", "Mapa", "Clientes", "Notas Fiscais", "Produtos", "Estoque", "Produção", "Detecção"]);
    expect(adminItems).toContain("Importações");
    expect(adminSystemItems).toContain("Importações");
  });

  it("mantem a ordem visual solicitada no menu lateral", () => {
    const source = readSidebar();

    expect(source).toContain('to: "/dashboard"');
    expect(source).toContain('label: "Dashboard"');
    expect(source).not.toContain('to: "/alertas"');
    expect(source).not.toContain('label: "Alertas"');
    expect(source).toContain('to: "/rotas"');
    expect(source).toContain('label: "Rotas"');
    expect(source).toContain('to: "/mapa"');
    expect(source).toContain('label: "Mapa"');
    expect(source).not.toContain('to: "/vendas"');
    expect(source).not.toContain('label: "Vendas"');
    expect(source).not.toContain('to: "/logistica"');
    expect(source).not.toContain('label: "Logística"');
    expect(source).toContain('to: "/produtos"');
    expect(source).toContain('label: "Produtos"');
    expect(source).toContain('to: "/estoque"');
    expect(source).toContain('label: "Estoque"');
    expect(source).toContain('to: "/producao"');
    expect(source).toContain('label: "Produção"');
    expect(source).toContain('to: "/importacoes/files"');
    expect(source).toContain('label: "Importações"');
    expect(source).toContain('to: "/processamentos"');
    expect(source).toContain('label: "Processamento"');
    expect(source).toContain('to: "/detections"');
    expect(source).toContain('label: "Detecção"');
    expect(source).not.toContain('to: "/administrativo"');
    expect(source).not.toContain('label: "Administrativo"');
    expect(source).not.toContain('to: "/relatorios"');
    expect(source).toContain('diretor');
    expect(source).toContain('to: "/clientes"');
    expect(source).toContain('label: "Clientes"');
    expect(source).toContain('to: "/notas-fiscais"');
    expect(source).toContain('label: "Notas Fiscais"');
  });

  it("remove itens fora do novo desenho do menu", () => {
    const source = readSidebar();

    expect(source).not.toContain('to: "/importacoes", label: "Importações", icon: FileUp');
    expect(source).not.toContain('label: "Files"');
    expect(source).not.toContain('label: "Finanças"');
    expect(source).not.toContain('label: "Relatórios"');
    expect(source).not.toContain('label: "Pendências"');
    expect(source).not.toContain('label: "Simulação"');
    expect(source).not.toContain('label: "Logística"');
    expect(source).not.toContain("item.children?.map");
  });

  it("mantem controles da sidebar acessiveis quando recolhida sem carregar a lista de notificacoes", () => {
    const source = readSidebar();

    expect(source).toContain("Expandir sidebar");
    expect(source).toContain("SidebarToggleArrow");
    expect(source).toContain("data-sidebar-toggle-arrow");
    expect(source).toContain("const Icon = open ? ChevronLeft : ChevronRight");
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
