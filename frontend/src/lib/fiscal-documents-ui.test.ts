import { describe, expect, it } from "vitest";
import fs from "node:fs";
import path from "node:path";

describe("exploração de fatos fiscais", () => {
  const route = fs.readFileSync(path.resolve("src/routes/notas-fiscais.tsx"), "utf8");
  const customerDialog = fs.readFileSync(path.resolve("src/components/CustomerConsumptionDialog.tsx"), "utf8");
  const fiscalDialog = fs.readFileSync(path.resolve("src/components/FiscalDocumentDialog.tsx"), "utf8");

  it("mantém busca com debounce, paginação e detalhe reutilizável", () => {
    expect(route).toContain("TEXT_SEARCH_DEBOUNCE_MS");
    expect(route).toContain("fetchFiscalDocuments(page, PAGE_SIZE");
    expect(route).toContain("<FiscalDocumentDialog");
  });

  it("apresenta detalhes da nota no mesmo padrão visual dos clientes", () => {
    expect(fiscalDialog.match(/<KpiCard/g)?.length).toBe(4);
    expect(fiscalDialog).toContain("Resumo da nota");
    expect(fiscalDialog).toContain("Quantidade × valor unitário");
    expect(fiscalDialog).toContain("data.calculatedTotalAmount");
    expect(fiscalDialog).toContain("item.calculatedAmount");
    expect(fiscalDialog).toContain("Itens da nota");
    expect(fiscalDialog).toContain("formatKpiCompactCurrency(data.calculatedTotalAmount)");
    expect(fiscalDialog).toContain("valueTooltip=");
  });

  it("explica que o cálculo é histórico de vendas e permite abrir a nota", () => {
    expect(customerDialog).toContain("Consumo em vendas — 30d");
    expect(customerDialog).toContain("Últimos 30d vs. 30d anteriores");
    expect(customerDialog).toContain("<KpiCard");
    expect(customerDialog.match(/<KpiCard/g)?.length).toBe(13);
    expect(customerDialog).toContain("<LineChart");
    expect(customerDialog).toContain("data.monthlyTimeline");
    expect(customerDialog).toContain("Últimos 12 meses");
    expect(customerDialog).toContain("Faturamento médio mensal");
    expect(customerDialog).toContain("Quantidade × valor unitário");
    expect(customerDialog).toContain("fetchCustomerProjection");
    expect(customerDialog).toContain("Projeção de impacto");
    expect(customerDialog).toContain("Peso mensal: realizado × projetado");
    expect(customerDialog).toContain("Faturamento mensal: realizado × projetado");
    expect(customerDialog).toContain("faixa de 95%");
    expect(customerDialog).not.toContain('tone="info"');
    expect(customerDialog).toContain("periodLabelClassName={projection.weight.monthlyChange > 0");
    expect(customerDialog).toContain("periodLabelClassName={projection.revenue.monthlyChange > 0");
    expect(customerDialog).toContain("projectionQualityClass");
    expect(customerDialog).toContain("formatKpiCompactNumber(projection.weight.monthlyChange)");
    expect(customerDialog).toContain("formatKpiCompactCurrency(projection.revenue.forecast[0]?.forecast");
    expect(customerDialog).toContain("valueTooltip=");
    expect(customerDialog).toContain('`${value > 0 ? "+" : ""}${percentageFormatter.format(value)}%`');
    expect(customerDialog).toContain("signedPercentage(projection.weight.monthlyChangePercentage)");
    expect(customerDialog).toContain("signedPercentage(projection.revenue.monthlyChangePercentage)");
    expect(customerDialog).toContain("formatDate(item.issueDate)");
    expect(customerDialog).toContain("Number.isNaN(date.getTime())");
    expect(customerDialog).toContain("<FiscalDocumentDialog");
  });
});
