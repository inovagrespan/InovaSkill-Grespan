import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { saveAuthToken } from "./auth";

describe("products api", () => {
  const localStorageMap = new Map<string, string>();
  const sessionStorageMap = new Map<string, string>();

  function createToken(exp: number): string {
    const header = Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })).toString("base64url");
    const payload = Buffer.from(JSON.stringify({ sub: "1", exp })).toString("base64url");
    return `${header}.${payload}.signature`;
  }

  beforeEach(() => {
    localStorageMap.clear();
    sessionStorageMap.clear();
    vi.stubGlobal("window", {
      localStorage: {
        getItem: (key: string) => localStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => localStorageMap.set(key, value),
        removeItem: (key: string) => localStorageMap.delete(key),
      },
      sessionStorage: {
        getItem: (key: string) => sessionStorageMap.get(key) ?? null,
        setItem: (key: string, value: string) => sessionStorageMap.set(key, value),
        removeItem: (key: string) => sessionStorageMap.delete(key),
      },
      location: { assign: vi.fn(), pathname: "/", search: "" },
    });
    saveAuthToken(createToken(Math.floor(Date.now() / 1000) + 60));
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it("busca produtos paginados na API com texto de pesquisa", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      Page: 1,
      PageSize: 20,
      Total: 1,
      Items: [
        { Id: "1", ErpCode: "10000122", OperationalCode: "6793", Name: "Massa Congelada", Type: "PA", Unit: "UN", GroupCode: "0022", Inventory: { AvailableQuantity: 12, OnHandQuantity: 15, CommittedQuantity: 3, StockValue: 42.5 } },
      ],
    }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const { fetchProducts } = await import("./importer-api");
    const result = await fetchProducts({ search: "massa", page: 1, pageSize: 20 });
    const requestedUrl = String(fetchMock.mock.calls[0][0]);

    expect(requestedUrl).toContain("/api/products?");
    expect(requestedUrl).toContain("search=massa");
    expect(result.total).toBe(1);
    expect(result.items[0]).toEqual({
      id: "1",
      erpCode: "10000122",
      operationalCode: "6793",
      name: "Massa Congelada",
      type: "PA",
      unit: "UN",
      groupCode: "0022",
      netWeightKg: null,
      grossWeightKg: null,
      gtin: "",
      inventory: { availableQuantity: 12, onHandQuantity: 15, committedQuantity: 3, stockValue: 42.5 },
    });
  });

  it("usa fallback demo quando a API de produtos nao responde", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => {
      throw new Error("fetch failed");
    }));

    const { fetchProducts } = await import("./importer-api");
    const result = await fetchProducts({ search: "pão", page: 1, pageSize: 20 });

    expect(result.total).toBeGreaterThan(0);
    expect(result.items[0].erpCode).toBeTruthy();
  });

  it("busca resumo de estoque na API", async () => {
    const fetchMock = vi.fn(async () => new Response(JSON.stringify({
      stockouts: 2,
      committedPercent: 10.5,
      lastDailyDate: "2026-05-31",
      lastProduction: 5076,
      lastOutbound: 5403,
      operationalBalance: -327,
    }), { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    const { fetchInventorySummary } = await import("./importer-api");
    const result = await fetchInventorySummary();

    expect(String(fetchMock.mock.calls[0][0])).toContain("/api/inventory/summary");
    expect(result).toEqual({
      stockouts: 2,
      committedPercent: 10.5,
      lastDailyDate: "2026-05-31",
      lastProduction: 5076,
      lastOutbound: 5403,
      operationalBalance: -327,
    });
  });
});
