import { describe, expect, it } from "vitest";
import { detectUploadDestination } from "./upload-destination-detector";

describe("upload destination detector", () => {
  it("detecta vendas por nome do arquivo", () => {
    const suggestion = detectUploadDestination("ITEM DE VENDA NOTAS FISCAIS DE SAIDA.xlsx");
    expect(suggestion.code).toBe("SALES_INVOICE");
    expect(suggestion.confidence).toBe("high");
  });

  it("detecta clientes por nome do arquivo", () => {
    const suggestion = detectUploadDestination("cadastro_clientes_2026.csv");
    expect(suggestion.code).toBe("CUSTOMER_LIST");
  });

  it("retorna baixa confiança quando nome é genérico", () => {
    const suggestion = detectUploadDestination("dados_maio.xlsx");
    expect(suggestion.confidence).toBe("low");
  });
});
