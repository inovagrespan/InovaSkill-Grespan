import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("customer impact layout", () => {
  it("usa o mesmo padrao de linha de metricas das demais abas financeiras", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain('<section className="metric-row">');
    expect(source).toContain("IMPACT_KPI_CARD_CLASS_NAME = \"p-3\"");
    expect(source).toContain("IMPACT_KPI_VALUE_CLASS_NAME = \"text-base sm:text-lg\"");
    expect(source).not.toContain("grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4");
  });

  it("mostra porcentagem de acao nos pontos de risco, crescimento e oportunidades", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain('formatImpactActionPercent(c, "risco")');
    expect(source).toContain('formatImpactActionPercent(c, "crescimento", true)');
    expect(source).toContain('formatImpactActionPercent(c, "oportunidades", true)');
  });
});
