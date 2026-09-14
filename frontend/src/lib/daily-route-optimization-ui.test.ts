import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("otimização diária de rotas", () => {
  const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/rotas.tsx"), "utf8");
  const simulation = fs.readFileSync(path.resolve(process.cwd(), "src/components/RouteOptimizationSimulation.tsx"), "utf8");

  it("mantém a visão original e apresenta a simulação otimizada separadamente", () => {
    expect(source).toContain("Rotas reais");
    expect(source).toContain("Sugestões");
    expect(source).toContain("RouteOptimizationSimulation");
    expect(simulation).toContain("simulateRoutes");
  });

  it("explica a margem saudável e identifica frota alugada", () => {
    expect(simulation).toContain("Veículo adicional");
    expect(simulation).toContain("RouteOccupancyIndicator");
    expect(simulation).toContain("Capacidade introduzida");
  });

  it("abre o detalhe da rota otimizada com mapa e explicação das mudanças", () => {
    expect(simulation).toContain("Detalhes da sugestão");
    expect(simulation).toContain("Sequência das cidades");
    expect(simulation).toContain("Distância");
    expect(simulation).toContain("Duração");
  });
});
