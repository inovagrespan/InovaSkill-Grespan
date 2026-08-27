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

  it("mantém somente o resumo e os itens, sem qualidade comercial", () => {
    expect(fiscalDialog).not.toContain("Qualidade comercial da venda");
    expect(fiscalDialog).not.toContain("Ticket médio do cliente");
    expect(fiscalDialog).not.toContain("Ticket da NF vs média");
    expect(fiscalDialog).not.toContain("data.commercialQuality");
  });

  it("mantém o diálogo de consumo integrado ao detalhe da nota", () => {
    expect(customerDialog).toContain("<KpiCard");
    expect(customerDialog.match(/<KpiCard/g)?.length).toBe(9);
    expect(customerDialog).toContain("<LineChart");
    expect(customerDialog).toContain("data.monthlyTimeline");
    expect(customerDialog).not.toContain("fetchCustomerProjection");
    expect(customerDialog).not.toContain("Projeção de impacto");
    expect(customerDialog).not.toContain("projetado");
    expect(customerDialog).toContain("Consumo em vendas — 30d");
    expect(customerDialog).toContain("Faturamento médio mensal");
    expect(customerDialog).toContain("formatDate(item.issueDate)");
    expect(customerDialog).toContain("Number.isNaN(date.getTime())");
    expect(customerDialog).toContain("<FiscalDocumentDialog");
  });
});
