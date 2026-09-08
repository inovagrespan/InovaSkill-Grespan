import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("otimização diária de rotas", () => {
  const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");

  it("mantém a visão original e apresenta a simulação otimizada separadamente", () => {
    expect(source).toContain('<TabsTrigger value="original">Original</TabsTrigger>');
    expect(source).toContain('value="optimization"');
    expect(source).toContain("Otimização por IA");
    expect(source).toContain("Aprovar simulação");
    expect(source).toContain("Rejeitar simulação");
    expect(source).toContain("decideDailyRouteOptimization");
    expect(source).toContain("não altera as rotas originais");
  });

  it("explica a margem saudável e identifica frota alugada", () => {
    expect(source).toContain("prioriza frota própria, combustível, autonomia e o menor custo de apoio");
    expect(source).not.toContain("apoio alugado a partir de 50%");
    expect(source).toContain('route.isRental ? "Alugado" : "Próprio"');
    expect(source).toContain("Diária estimada do aluguel");
    expect(source).toContain("dimensionado pelo peso remanejado e pelo menor custo total");
    expect(source).not.toContain("Janela operacional");
    expect(source).not.toContain("Retorno dentro da meta");
  });

  it("abre o detalhe da rota otimizada com mapa e explicação das mudanças", () => {
    const optimizedModalStart = source.indexOf('<Dialog open={optimizedDetailsOpen}');
    const beforeOptimizedModal = source.slice(0, optimizedModalStart);
    const optimizedModal = source.slice(optimizedModalStart);

    expect(source).toContain("openOptimizedDetails");
    expect(source).toContain("Detalhes da rota otimizada");
    expect(source).toContain("Novo percurso");
    expect(source).toContain("O que mudou");
    expect(source).toContain("Remanejada de outra rota original");
    expect(source).toContain("Transporte adicional");
    expect(source).toContain("fetchOptimizedRouteRoadPath");
    expect(source).toContain('optimization?.result?.importId');
    expect(source).toContain('optimizedRoadPath.stops.length - 2');
    expect(source).toContain('cliente(s)');
    expect(source).toContain("optimizedCustomerGroups");
    expect(source).toContain("group.municipalityName");
    expect(source).toContain("customer.label");
    expect(beforeOptimizedModal).not.toContain("Combustível estimado");
    expect(optimizedModal).toContain("Caminhão utilizado");
    expect(optimizedModal).toContain("Quilômetros rodados");
    expect(optimizedModal).toContain("Tempo para completar a rota");
    expect(optimizedModal).toContain("Combustível estimado");
    expect(optimizedModal).toContain("formatFuelConsumptionRange");
    expect(optimizedModal).toContain("Consumo não cadastrado");
    expect(optimizedModal).toContain("Tanque e autonomia");
    expect(optimizedModal).toContain("Autonomia restante com reserva");
    expect(optimizedModal).toContain("Execute uma nova otimização");
  });
});
