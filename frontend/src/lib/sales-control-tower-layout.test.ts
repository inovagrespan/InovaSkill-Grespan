import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("sales control tower layered experience", () => {
  it("cria aba própria e mantém o fluxo legado de notas fiscais reutilizável", () => {
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/vendas.tsx"), "utf8");
    const sidebar = fs.readFileSync(path.resolve(process.cwd(), "src/components/AppSidebar.tsx"), "utf8");
    expect(route).toContain('createFileRoute("/vendas")');
    expect(route).toContain("SalesControlTower");
    expect(route).toContain("export function VendasPage");
    expect(sidebar).toContain('{ to: "/vendas", label: "Vendas"');
  });

  it("exibe os indicadores solicitados e quatro níveis progressivos", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/components/SalesControlTower.tsx"), "utf8");
    for (const title of ["Volume de Vendas", "Volume de Bonificação", "Consumo de Clientes Ativos", "Consumo por Produto", "Prospecção × Fechamento", "Tempo de Conversão", "Sazonalidade de Consumo", "Taxa de Retenção", "Erro de Previsão (MAPE)", "Preço Médio por Vendedor/Região", "Ticket Médio", "Lifetime Value (LTV)"]) expect(source).toContain(title);
    expect(source).toContain("Nível 2 · Entender o resultado");
    expect(source).toContain("Nível 3 · Investigação");
    expect(source).toContain("Nível 4 · Tomada de decisão");
    expect(source).toContain("SalesTrendChart");
    expect(source).toContain("buildContextualSalesRecommendation");
    expect(source).not.toContain("LogisticsRegionMap");
  });
});
