import { describe, expect, it } from "vitest";
import { parseManualHeaders } from "./parse-manual-headers";

describe("parseManualHeaders", () => {
  it("deve parsear headers separados por quebra de linha", () => {
    const input = "documento\ndata\ncliente\nvalor_total";
    expect(parseManualHeaders(input)).toEqual(["documento", "data", "cliente", "valor_total"]);
  });

  it("deve parsear linha copiada do Excel com TAB", () => {
    const input = "Documento\tSERIE\tCLIENTE\tLOJA\tNOME";
    expect(parseManualHeaders(input)).toEqual(["Documento", "SERIE", "CLIENTE", "LOJA", "NOME"]);
  });

  it("deve parsear valores separados por ponto-e-vírgula", () => {
    const input = "documento;data;cliente;valor_total";
    expect(parseManualHeaders(input)).toEqual(["documento", "data", "cliente", "valor_total"]);
  });

  it("deve parsear valores separados por vírgula", () => {
    const input = "documento,data,cliente,valor_total";
    expect(parseManualHeaders(input)).toEqual(["documento", "data", "cliente", "valor_total"]);
  });

  it("deve remover espaços, entradas vazias e duplicados", () => {
    const input = " documento , , data \n cliente \n documento \t valor_total ";
    expect(parseManualHeaders(input)).toEqual(["documento", "data", "cliente", "valor_total"]);
  });

  it("deve retornar vazio quando não houver headers válidos", () => {
    expect(parseManualHeaders("\n\n ; ; \t ,")).toEqual([]);
  });
});
