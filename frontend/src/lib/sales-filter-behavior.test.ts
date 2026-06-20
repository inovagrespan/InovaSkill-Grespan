import fs from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

function read(relativePath: string): string {
  return fs.readFileSync(path.resolve(process.cwd(), relativePath), "utf8");
}

describe("sales filter behavior", () => {
  it("usa debounce, abort e protecao contra respostas antigas nos filtros principais", () => {
    const routeSource = read("src/routes/vendas.tsx");
    const apiSource = read("src/lib/importer-api.ts");

    expect(routeSource).toContain("FILTER_DEBOUNCE_MS = 300");
    expect(routeSource).toContain("SEARCH_MIN_LENGTH = 2");
    expect(routeSource).toContain("useDebouncedValue(documentNumberInput, FILTER_DEBOUNCE_MS)");
    expect(routeSource).toContain("useDebouncedValue(productCodeInput, FILTER_DEBOUNCE_MS)");
    expect(routeSource).toContain("new AbortController()");
    expect(routeSource).toContain("requestIdRef");
    expect(routeSource).toContain("signal?.aborted");
    expect(apiSource).toContain("signal?: AbortSignal");
    expect(apiSource).toContain("groupBy");
  });

  it("mantem filtros textuais como busca livre e oferece limpeza rapida", () => {
    const routeSource = read("src/routes/vendas.tsx");

    expect(routeSource).not.toContain("sales-document-options");
    expect(routeSource).not.toContain("sales-product-options");
    expect(routeSource).toContain("value={documentNumberInput}");
    expect(routeSource).toContain("value={productCodeInput}");
    expect(routeSource).toContain("setDocumentNumberInput(\"\")");
    expect(routeSource).toContain("setProductCodeInput(\"\")");
  });

  it("persiste estado e cacheia consultas recentes da tela de vendas", () => {
    const routeSource = read("src/routes/vendas.tsx");

    expect(routeSource).toContain("SALES_STATE_STORAGE_KEY");
    expect(routeSource).toContain("sessionStorage.setItem");
    expect(routeSource).toContain("readPersistedSalesState");
    expect(routeSource).toContain("SALES_CACHE_TTL_MS");
    expect(routeSource).toContain("cacheRef.current");
  });

  it("exibe ranking detalhado com documento e sem coluna de variacao", () => {
    const routeSource = read("src/routes/vendas.tsx");

    expect(routeSource).toContain("Documento/Nota Fiscal");
    expect(routeSource).toContain("buildSalesDocumentLabel");
    expect(routeSource).not.toContain("<TableHead>Variação</TableHead>");
  });
});
