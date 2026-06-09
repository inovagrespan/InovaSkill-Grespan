import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function readClientesRoute(): string {
  return fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
}

describe("clientes - refinamento visual do modal de detalhes", () => {
  it("mantem botao de fechar dentro da area util com alvo de clique confortavel", () => {
    const source = readClientesRoute();

    expect(source).toContain("[&>button]:right-3");
    expect(source).toContain("[&>button]:top-3");
    expect(source).toContain("[&>button]:h-9");
    expect(source).toContain("[&>button]:w-9");
    expect(source).toContain('DialogHeader className="pr-10 sm:pr-12"');
  });

  it("separa indicadores em grupos financeiro e operacional", () => {
    const source = readClientesRoute();

    expect(source).toContain("Indicadores financeiros");
    expect(source).toContain("Indicadores operacionais");
  });

  it("usa descricao explicita para frequencia media", () => {
    const source = readClientesRoute();

    expect(source).toContain("Compra em média a cada");
  });

  it("garante area vertical maior no grafico de evolucao temporal", () => {
    const source = readClientesRoute();

    expect(source).toContain("h-[320px]");
    expect(source).toContain("sm:h-[340px]");
    expect(source).toContain("bottom: 20");
  });

  it("troca tabela do comparativo por paineis visuais com estado de dados insuficientes", () => {
    const source = readClientesRoute();

    expect(source).toContain("Dados insuficientes para comparação");
    expect(source).toContain("comparablePeriods.length === 0");
    expect(source).toContain("formatVariationPercent(item.variationPercent)");
    expect(source).not.toContain("<TableHead>Valor atual</TableHead>");
  });

  it("simplifica a lista com leitura de tendência e resume o gráfico por período", () => {
    const source = readClientesRoute();

    expect(source).toContain("<TableHead>Leitura do período</TableHead>");
    expect(source).toContain("resolveRankingTrend(item.variationPercent)");
    expect(source).toContain("Média por");
    expect(source).toContain("Tendência do período");
    expect(source).toContain("Variação média por");
    expect(source).toContain("Primeiro vs último ponto");
  });

  it("remove inteligencia comercial e produtos mais comprados do modal", () => {
    const source = readClientesRoute();

    expect(source).not.toContain("Inteligência Comercial");
    expect(source).not.toContain("Saúde do Cliente");
    expect(source).not.toContain("Recomendação Comercial");
    expect(source).not.toContain("Produtos Mais Relevantes");
    expect(source).not.toContain("<CardTitle>Produtos mais comprados</CardTitle>");
    expect(source).not.toContain("fetchCustomerTopProducts");
  });
});
