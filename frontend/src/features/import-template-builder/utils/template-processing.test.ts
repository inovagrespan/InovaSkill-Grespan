import { describe, expect, it } from "vitest";
import type { BuilderMapping, BuilderTargetField, FieldRuleConfig } from "../types";
import {
  buildProcessedPreview,
  buildValidationChecklist,
  defaultRuleConfig,
  normalizeDate,
  normalizeNumber,
  normalizeText,
} from "./template-processing";

const baseConfig: FieldRuleConfig = { ...defaultRuleConfig };

describe("template-processing", () => {
  it("normaliza moeda brasileira", () => {
    expect(normalizeNumber("R$ 1.234,56", baseConfig)).toBe("1234.56");
  });

  it("normaliza número com casas configuradas", () => {
    expect(normalizeNumber("1.234,567", { ...baseConfig, decimalPlaces: "3" })).toBe("1234.567");
  });

  it("normaliza data pelos formatos aceitos", () => {
    expect(normalizeDate("31/05/2026", { ...baseConfig, dateFormats: "dd/MM/yyyy, yyyy-MM-dd" })).toBe("2026-05-31");
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

  it("marca template inválido quando obrigatório não foi mapeado", () => {
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

  it("marca template válido sem pendências", () => {
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
});
