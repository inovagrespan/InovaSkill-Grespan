import { describe, expect, it } from "vitest";
import type { BuilderMapping, BuilderTargetField, FieldRuleConfig } from "../types";
import {
  buildProcessedPreview,
  buildTransformRulesFromConfig,
  buildValidationChecklist,
  defaultRuleConfig,
  hasBlockingValidation,
  normalizeDate,
  normalizeNumber,
  normalizeText,
} from "./template-processing";

const baseConfig: FieldRuleConfig = { ...defaultRuleConfig };

describe("template-processing", () => {
  it("normaliza moeda brasileira", () => {
    expect(normalizeNumber("R$ 1.234,56", baseConfig)).toBe("1234.56");
  });

  it("trata ponto unico com tres digitos como milhar brasileiro", () => {
    expect(normalizeNumber("1.234", baseConfig)).toBe("1234.00");
    expect(normalizeNumber("3.32", { ...baseConfig, detectBrazilianFormat: false, detectInternationalFormat: true })).toBe("3.32");
  });

  it("normaliza numero com casas configuradas", () => {
    expect(normalizeNumber("1.234,567", { ...baseConfig, decimalPlaces: "3" })).toBe("1234.567");
  });

  it("rejeita valores fora dos limites de negocio", () => {
    expect(() => normalizeNumber("-0,01", baseConfig)).toThrow("negativo");
    expect(() => normalizeNumber("100,01", { ...baseConfig, maxValue: "100" })).toThrow("maior");
    expect(() => normalizeNumber("0", { ...baseConfig, positiveOnly: true })).toThrow("positivo");
  });

  it("normaliza data pelos formatos aceitos", () => {
    expect(normalizeDate("31/05/2026", { ...baseConfig, dateFormats: "dd/MM/yyyy, yyyy-MM-dd" })).toBe("2026-05-31");
  });

  it("rejeita datas impossiveis mesmo quando o formato parece correto", () => {
    expect(() => normalizeDate("31/02/2026", { ...baseConfig, dateFormats: "dd/MM/yyyy" })).toThrow("formatos aceitos");
  });

  it("aplica regras de texto", () => {
    expect(normalizeText("  José & Cia  ", { ...baseConfig, uppercase: true, removeSpecialCharacters: true })).toBe("JOSE  CIA");
  });

  it("monta preview processado com status ok", () => {
    const fields: BuilderTargetField[] = [
      { name: "unitprice", displayName: "Valor Unitário", required: true, dataType: "currency" },
    ];
    const mappings: Record<string, BuilderMapping> = {
      unitprice: {
        sourceColumnName: "Vlr. Unit.",
        targetFieldName: "unitprice",
        isRequired: true,
        defaultValue: "",
        transformRules: [],
      },
    };

    const preview = buildProcessedPreview({
      rows: [{ "Vlr. Unit.": "R$ 1.234,56" }],
      mappings,
      fields,
      configs: { unitprice: baseConfig },
    });

    expect(preview[0]).toMatchObject({
      sourceColumnName: "Vlr. Unit.",
      processedValue: "1234.56",
      status: "ok",
    });
  });

  it("usa valor default quando origem obrigatoria vem vazia", () => {
    const fields: BuilderTargetField[] = [
      { name: "city", displayName: "Cidade", required: true, dataType: "text" },
    ];
    const mappings: Record<string, BuilderMapping> = {
      city: {
        sourceColumnName: "Cidade",
        targetFieldName: "city",
        isRequired: true,
        defaultValue: "Sem cidade",
        transformRules: [],
      },
    };

    const preview = buildProcessedPreview({
      rows: [{ Cidade: " " }],
      mappings,
      fields,
      configs: { city: baseConfig },
    });

    expect(preview[0]).toMatchObject({
      processedValue: "Sem cidade",
      status: "ok",
    });
  });

  it("marca preview com erro para dado contraditorio em campo numerico", () => {
    const fields: BuilderTargetField[] = [
      { name: "quantity", displayName: "Quantidade", required: true, dataType: "number" },
    ];
    const mappings: Record<string, BuilderMapping> = {
      quantity: {
        sourceColumnName: "Quantidade",
        targetFieldName: "quantity",
        isRequired: true,
        defaultValue: "",
        transformRules: [],
      },
    };

    const preview = buildProcessedPreview({
      rows: [{ Quantidade: "-1" }],
      mappings,
      fields,
      configs: { quantity: baseConfig },
    });

    expect(preview[0]).toMatchObject({
      status: "error",
      processedValue: "",
    });
  });

  it("marca template invalido quando obrigatorio nao foi mapeado", () => {
    const checklist = buildValidationChecklist({
      name: "Template",
      importFileTypeId: "SALES_INVOICE",
      headers: ["CLIENTE"],
      mappings: {},
      fields: [{ name: "customername", displayName: "Cliente", required: true, dataType: "text" }],
      preview: [],
    });

    expect(checklist.some((item) => item.label === "Campos obrigatórios mapeados" && item.status === "error")).toBe(true);
  });

  it("bloqueia headers e mapeamentos duplicados que causariam importacao ambigua", () => {
    const checklist = buildValidationChecklist({
      name: "Template",
      importFileTypeId: "SALES_INVOICE",
      headers: ["CLIENTE", "cliente", "TOTAL"],
      mappings: {
        customername: {
          sourceColumnName: "CLIENTE",
          targetFieldName: "customername",
          isRequired: true,
          defaultValue: "",
          transformRules: [],
        },
        customeralias: {
          sourceColumnName: "CLIENTE",
          targetFieldName: "customeralias",
          isRequired: false,
          defaultValue: "",
          transformRules: [],
        },
      },
      fields: [
        { name: "customername", displayName: "Cliente", required: true, dataType: "text" },
        { name: "customeralias", displayName: "Cliente alternativo", required: false, dataType: "text" },
      ],
      preview: [],
    });

    expect(hasBlockingValidation(checklist)).toBe(true);
    expect(checklist).toContainEqual(expect.objectContaining({ label: "Sem headers duplicados", status: "error" }));
    expect(checklist).toContainEqual(expect.objectContaining({ label: "Sem mapeamentos duplicados", status: "error" }));
  });

  it("marca template valido sem pendencias", () => {
    const checklist = buildValidationChecklist({
      name: "Template",
      importFileTypeId: "SALES_INVOICE",
      headers: ["CLIENTE"],
      mappings: {
        customername: {
          sourceColumnName: "CLIENTE",
          targetFieldName: "customername",
          isRequired: true,
          defaultValue: "",
          transformRules: [],
        },
      },
      fields: [{ name: "customername", displayName: "Cliente", required: true, dataType: "text" }],
      preview: [{ rowIndex: 0, sourceColumnName: "CLIENTE", targetFieldName: "customername", targetFieldLabel: "Cliente", originalValue: "ACME", processedValue: "ACME", status: "ok", message: "OK" }],
    });

    expect(checklist.every((item) => item.status === "ok")).toBe(true);
  });

  it("gera regras de transformacao na ordem observavel que sera enviada para API", () => {
    const ruleCodeToId = new Map([
      ["Trim", "rule-trim"],
      ["RemoveSpecialCharacters", "rule-clean"],
      ["UpperCase", "rule-upper"],
      ["BrazilianCurrency", "rule-money"],
    ]);

    const rules = buildTransformRulesFromConfig({
      field: { name: "totalamount", displayName: "Valor total", required: true, dataType: "currency" },
      config: { ...baseConfig, trim: true, removeSpecialCharacters: true, uppercase: true, maxValue: "10000" },
      ruleCodeToId,
    });

    expect(rules.map((rule) => rule.transformRuleId)).toEqual(["rule-trim", "rule-clean", "rule-upper", "rule-money"]);
    expect(rules[3].parametersJson).toMatchObject({ maxValue: 10000, allowNegative: false });
  });
});
