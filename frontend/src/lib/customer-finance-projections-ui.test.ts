import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("customer finance projections UI", () => {
  it("exibe custos simulados na tabela e no grafico de projecoes financeiras", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain("calculateSimulatedProjectedCost");
    expect(source).toContain("calculateProjectedMarginPercent");
    expect(source).toContain("Custo simulado até integração");
    expect(source).toContain("<TableHead className=\"text-right\">Custo simulado</TableHead>");
    expect(source).toContain("<TableHead className=\"text-right\">Margem proj.</TableHead>");
    expect(source).toContain("formatCurrency(projectedCost)");
    expect(source).toContain("formatDecimal(projectedMargin)");
  });
});
