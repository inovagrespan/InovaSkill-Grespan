import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { formatImpactActionPercent } from "./customer-finance-impact";

describe("customer finance impact tables", () => {
  it("abre risco, crescimento e oportunidades em tela limpa dedicada", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");
    const route = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes-impacto.tsx"), "utf8");

    expect(source).toContain('to="/clientes-impacto"');
    expect(source).toContain('tipo: "risco"');
    expect(source).toContain('tipo: "crescimento"');
    expect(source).toContain('tipo: "oportunidades"');
    expect(source.match(/Ver todos/g)?.length ?? 0).toBeGreaterThanOrEqual(3);
    expect(route).toContain('createFileRoute("/clientes-impacto")');
    expect(route).toContain("Voltar para impacto");
    expect(route).not.toContain("<TableHead>Atenção</TableHead>");
  });

  it("normaliza percentuais e nomes vindos da API antes de exibir impacto", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/lib/customer-finance-impact.ts"), "utf8");

    expect(source).toContain("getImpactCustomerName");
    expect(source).toContain("getImpactPercent");
    expect(source).toContain("formatImpactActionPercent");
    expect(source).toContain("getImpactActionScorePercent");
    expect(source).toContain("Crescimento12M");
    expect(source).toContain("ClienteNome");
  });

  it("usa score como porcentagem quando a variacao da acao nao veio da API", () => {
    expect(formatImpactActionPercent({ variacaoPercentual: null, scoreRisco: 72 }, "risco")).toBe("72%");
    expect(formatImpactActionPercent({ crescimento12M: null, scorePotencial: 94 }, "crescimento", true)).toBe("94%");
    expect(formatImpactActionPercent({ crescimento12M: 18.3, scorePotencial: 94 }, "oportunidades", true)).toBe("+18.3%");
    expect(formatImpactActionPercent({ crescimento12M: null }, "oportunidades", true)).toBe("-");
  });
});
