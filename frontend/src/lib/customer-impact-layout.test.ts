import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

describe("customer impact layout", () => {
  it("usa o mesmo padrao de linha de metricas das demais abas financeiras", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain('<section className="metric-row">');
    expect(source).toContain("IMPACT_KPI_CARD_CLASS_NAME = \"p-3\"");
    expect(source).not.toContain("IMPACT_KPI_VALUE_CLASS_NAME");
    expect(source).not.toContain("valueClassName={IMPACT_KPI_VALUE_CLASS_NAME}");
    expect(source).not.toContain("grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4");
  });

  it("mostra porcentagem de acao nos pontos de risco, crescimento e oportunidades", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain('formatImpactActionPercent(c, "risco")');
    expect(source).toContain('formatImpactActionPercent(c, "crescimento", true)');
    expect(source).toContain('formatImpactActionPercent(c, "oportunidades", true)');
  });

  it("abre sugestoes de acoes para cada cliente em risco", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain("buildRiskCustomerActionSuggestions");
    expect(source).toContain("setRiskActionCustomer(c)");
    expect(source).toContain("Sugestões de ações");
    expect(source).toContain("Plano recomendado para sanar o risco");
    expect(source).toContain("Priorizar contato comercial");
  });

  it("permite abrir abas de clientes diretamente pelo dashboard", () => {
    const source = fs.readFileSync(path.resolve(process.cwd(), "src/routes/clientes.tsx"), "utf8");

    expect(source).toContain("aba: isClientesTab(search.aba) ? search.aba : undefined");
    expect(source).toContain('const [activeTab, setActiveTab] = useState<ClientesTab>(aba ?? "impacto")');
    expect(source).toContain("search: (prev) => ({ ...prev, aba: tab })");
  });
});
